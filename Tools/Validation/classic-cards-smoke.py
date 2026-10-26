#!/usr/bin/env python3
"""Real-input browser smoke for the 96-card Fantasy Masters classic revision.

Uses only rendered OCR, real pointer/touch controls and passive loader/audio
observations. The checked-in catalog identifies visible cards; no Unity state,
RNG, saved battle payload or hidden opponent hand is inspected or changed.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import functools
import hashlib
import http.server
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import threading
import time

SPEC = importlib.util.spec_from_file_location("firstgame_web_smoke", Path(__file__).with_name("web-smoke.py"))
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
NETWORK_SPEC=importlib.util.spec_from_file_location("classic_network_evidence",Path(__file__).with_name("classic-network-evidence.py"))
NETWORK=importlib.util.module_from_spec(NETWORK_SPEC)
NETWORK_SPEC.loader.exec_module(NETWORK)
BASE.classify_network_failures=NETWORK.classify_network_failures
ROOT, GAME = BASE.ROOT, "FantasyCards"
BASE.TITLES[GAME] = "판타지마스터즈"


def authored_name_matches(name, visible):
    """A one-syllable authored name must not match part of a longer name."""
    return re.search(r"(?<![가-힣])" + re.escape(BASE.compact(name)) + r"(?![가-힣])",
        BASE.compact(visible)) is not None


class ClassicSmoke(BASE.Smoke):
    def __init__(self, page, output, args):
        super().__init__(page, GAME, output, args)
        source = ROOT / "Tools/Data/ClassicCards/selection.json"
        self.cards = json.loads(source.read_text())["cards"]
        self.names = sorted((c["name"] for c in self.cards), key=len, reverse=True)
        self.by_name = {c["name"]: c for c in self.cards}
        self.report.update({"suite":"classic-cards-smoke-v1", "classicEvidence":{},
            "harness":{"file":str(Path(__file__).relative_to(ROOT)),
                "sha256":hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                "sharedHarnessSha256":hashlib.sha256(Path(BASE.__file__).read_bytes()).hexdigest()},
            "sourceCatalog":{"file":str(source.relative_to(ROOT)), "sha256":hashlib.sha256(source.read_bytes()).hexdigest(),
                "cards":len(self.cards), "purpose":"Static identity and expected filter totals only; no runtime state."},
            "networkClassifier":{"file":str(Path(NETWORK.__file__).relative_to(ROOT)),
                "sha256":hashlib.sha256(Path(NETWORK.__file__).read_bytes()).hexdigest(),
                "restoreCheckId":NETWORK.RESTORE_CHECK_ID},
            "coverage":{"included":["source art detail", "element, rarity and soul filters", "four starter choices",
                "six-card whole-hand replacement", "real terrain and unit placement", "both preparations and automatic coin combat",
                "save, full page reload and Continue", "seven passive BGM windows"],
                "excluded":["all 96 card effects", "all three encounter victories", "physical mobile hardware", "performance benchmarks"],
                "visualAssessment":"Original browser frames require separate direct review; OCR does not certify artwork quality."},
            "visualReview":{"status":"pending", "method":"Direct review of original screenshots from this exact runtime."}})
        self.hand_scroll = "left"

    def click(self, x, y, label, reference=False):
        super().click(x, y, label, reference=reference)
        if self.args.profile == "desktop":
            # A newly created right-side tile can briefly receive the shared
            # helper's edge pointer move. Use a blank in-canvas header point
            # and let Unity consume that real move before reading the screen.
            px,py,dx,dy = self.canvas_point(.01,.005)
            self.page.mouse.move(px,py)
            self.page.wait_for_timeout(350)
            self.report["input"].append({"label":"Dismiss hover help at the blank canvas header",
                "type":"mouse-move","canvasX":round(dx,2),"canvasY":round(dy,2)})
            self.persist()

    def wait_title(self):
        if self.args.profile == "desktop":
            px,py,_,_ = self.canvas_point(.01,.005)
            self.page.mouse.move(px,py)
            self.page.wait_for_timeout(350)
        return super().wait_title()

    def wait_text(self, expected, timeout=None, region=None, psm=11, language="kor+eng", match_all=False,
                  literal=False, button_text=False, allow_pause=True, whole_name=False):
        self.last_observation_method="ocr"
        try:
            visible=super().wait_text(expected,timeout=timeout,region=region,psm=psm,language=language,
                match_all=match_all,literal=literal,button_text=button_text,allow_pause=False)
            expected_list=[expected] if isinstance(expected,str) else list(expected)
            if whole_name and not any(authored_name_matches(name,visible) for name in expected_list):
                raise AssertionError(f"OCR contains no complete authored card name; partial syllable matches rejected: {visible!r}")
            return visible
        except AssertionError as failure:
            if not allow_pause or not self.args.pause_on_ocr_failure: raise
            expected_list=[expected] if isinstance(expected,str) else list(expected)
            index=len(self.report.setdefault("directReviewPauses",[]))+1
            frame=self.snapshot(f"direct-review-pause-{index:03}")
            resume=self.output/f"direct-review-resume-{index:03}.json"
            entry={"status":"paused","expected":expected_list,"region":region,"psm":psm,
                "ocrFailure":str(failure),"frame":frame,"resumeFile":str(resume),"wholeName":whole_name,
                "allowedRecovery":"Same expected labels with a corrected ROI, or explicit direct review of this exact original frame."}
            self.report["directReviewPauses"].append(entry);self.persist()
            print(f"{GAME}: PAUSED for same-label ROI or explicit original-frame visual confirmation at {resume}",flush=True)
            deadline=time.monotonic()+1200
            while time.monotonic()<deadline:
                self.page.wait_for_timeout(1000)
                if not resume.exists(): continue
                recovery=json.loads(resume.read_text())
                if recovery.get("expected")!=expected_list:
                    raise AssertionError("Direct review must retain the exact original expected labels")
                if recovery.get("mode")=="visual-confirmation":
                    confirmed=recovery.get("confirmedText","")
                    found=[authored_name_matches(word,confirmed) if whole_name else
                        (word in confirmed if literal else BASE.compact(word) in BASE.compact(confirmed)) for word in expected_list]
                    if (recovery.get("reviewedFrameSha256")!=frame["sha256"] or not recovery.get("reviewer") or
                            not recovery.get("reviewedUtc") or not (all(found) if match_all else any(found))):
                        raise AssertionError("Visual confirmation requires the exact original frame hash, reviewer, date and observed labels")
                    entry.update({"status":"confirmed-by-direct-original-frame-review","recovery":recovery})
                    self.report.setdefault("visualConfirmations",[]).append({**recovery,"frame":frame,"originalOcrFailure":str(failure),
                        "method":"Direct inspection of the original rendered frame; explicitly not automatic OCR acceptance."})
                    self.last_observation_method="direct-original-frame-review"
                    self.persist()
                    return confirmed
                corrected=recovery.get("region",region); new_psm=recovery.get("psm",psm)
                if (not isinstance(corrected,list) or len(corrected)!=4 or not(0<=corrected[0]<corrected[2]<=1 and
                        0<=corrected[1]<corrected[3]<=1) or new_psm not in (6,7,11)):
                    raise AssertionError("OCR recovery requires a bounded visible ROI and supported segmentation")
                entry.update({"status":"resumed-with-same-expectation","recovery":recovery});self.persist()
                return self.wait_text(expected_list,timeout=timeout,region=corrected,psm=new_psm,language=language,
                    match_all=match_all,literal=literal,button_text=button_text,whole_name=whole_name)
            entry["status"]="recovery-timeout";self.persist()
            raise failure

    def resolutions(self, prefix):
        self.interaction_viewport()
        shot = self.snapshot(prefix + "-" + self.args.interaction_resolution)
        w, h = map(int, self.args.interaction_resolution.split("x"))
        box, scale = shot["canvas"], min(w / 16, h / 9)
        self.check(abs(box["width"] - scale*16) <= 1 and abs(box["height"] - scale*9) <= 1 and
            box["x"] >= -1 and box["y"] >= -1 and box["x"]+box["width"] <= w+1 and box["y"]+box["height"] <= h+1,
            "Requested viewport contains the aspect-fitted canvas", box)
        self.check(min(shot["channelStddev"]) > 8, "Canvas contains nonblank rendered pixels", shot["channelStddev"])
        if self.args.profile == "mobile-landscape": self.snapshot(prefix + "-full-viewport", full_viewport=True)

    def evidence(self, key, value):
        self.report["classicEvidence"][key] = value
        self.persist()

    def filter(self, key, enum_value, label, expected_count):
        self.click_control(key, "Open " + label + " filter")
        position = enum_value + 1
        self.click(.16 + position%4*.226, .2975 + position//4*.145, "Choose " + label)
        visible = self.wait_text(f"조건에 맞는 카드 {expected_count}종", region=self.region("collectionCount"), psm=7)
        self.wait_text(label, region=self.region("filterLabels"))
        shot = self.snapshot("filter-" + key + "-" + str(enum_value))
        self.click_control("filterReset", "Reset actual collection filters")
        self.wait_text("조건에 맞는 카드 96종", region=self.region("collectionCount"), psm=7)
        return {"filter":label, "expectedCountFromStaticCatalog":expected_count, "visible":visible, "screenshot":shot}

    def collection(self):
        self.click_control("titleCollection", "Open the actual card collection")
        self.wait_text("조건에 맞는 카드 96종", region=self.region("collectionCount"), psm=7)
        self.snapshot("collection-96-original-cards")
        # Raw DB SoulType4 is forest; rarity4 is Rare. Zero soul is all terrain.
        forest = sum(int(c["fields"]["SoulType"]) == 4 for c in self.cards)
        rare = sum(int(c["fields"]["RareType"]) == 4 for c in self.cards)
        terrain = sum(int(c["fields"]["Category"]) == 1 for c in self.cards)
        def soul_cost(card):
            encoded = card["fields"]["RequireSoul"]
            return sum(int(encoded[i+2:i+4]) for i in range(0,len(encoded),4) if int(encoded[i:i+2]) != 1)
        high_cost = sum(soul_cost(c) == 7 for c in self.cards)
        checks = [self.filter("filterElement", 3, "숲", forest),
                  self.filter("filterRarity", 3, "레어", rare),
                  self.filter("filterCost", 0, "소울 · 0", terrain),
                  self.filter("filterCost", 7, "소울 · 7", high_cost)]
        self.evidence("collection_filters", checks)
        self.click_control("collectionFirst", "Open the first original card's explanation")
        name = self.card_name(self.region("detailName"))
        self.wait_text("LP", region=self.region("detailCost"), language="eng")
        detail = self.snapshot("original-card-explanation")
        self.click_control("originalCard", "Show the complete original printed card")
        self.wait_text([name, "원본 카드"], region=self.region("originalHeader"), match_all=True)
        full = self.snapshot("original-card-full-print")
        card = self.by_name[name]
        art = ROOT / "Games/FantasyCards/Assets/Resources" / (card["artResource"] + ".png")
        self.evidence("original_card", {"name":name, "sourceId":card["sourceId"], "sourceUrl":card["sourceUrl"],
            "localImageSha256":hashlib.sha256(art.read_bytes()).hexdigest(), "detail":detail, "printedCard":full})
        self.click_control("back", "Return to the card explanation")
        self.click_control("detailBack", "Return to collection")
        self.click_control("collectionTitle", "Return to title")
        self.wait_title()

    def card_name(self, region):
        visible = self.wait_text(self.names, region=region, psm=7, whole_name=True)
        name = next((name for name in self.names if authored_name_matches(name,visible)), None)
        self.check(name is not None, "Visible original card name matches the static catalog",
                   {"name":name,"observedText":visible,"method":self.last_observation_method})
        return name

    def opening_hand(self, label):
        names = []
        for index in range(6):
            self.click(.035+(index+.5)*.155, .49, f"Inspect opening card {index+1}: {label}")
            names.append(self.card_name(self.region("detailName")))
            self.click_control("detailBack", "Return to the six-card opening hand")
        shot = self.snapshot("opening-" + label)
        return {"cards":names, "screenshot":shot}

    def start_battle(self):
        self.click_control("titleNew", "Start a new classic duel")
        self.wait_text("결투에 함께할 아바타", region=self.region("header"))
        self.snapshot("four-cosmetic-avatars")
        self.click_control("avatarFirst", "Choose Lyria's cosmetic avatar")
        self.click_control("avatarNext", "Choose a starter deck")
        self.wait_text("첫 결투를 함께할 덱", region=self.region("header"))
        self.wait_text(["생명의 전열", "강철의 진격", "심연의 파도", "황금의 성채"],
            region=(.035,.26,.965,.845), match_all=True)
        self.wait_text("50장", region=(.035,.10,.965,.23))
        shot = self.snapshot("four-fifty-card-starter-decks")
        self.click_control("starterFirst", "Select the actual forest/light 50-card starter")
        self.click_control("starterNext", "Proceed with that selected starter")
        self.wait_text("별빛 시험의 세 결투", region=self.region("header"))
        self.snapshot("three-encounter-choices")
        self.click_control("encounterFirst", "Choose the first encounter")
        self.click_control("encounterNext", "Shuffle and deal the selected 50-card deck")
        self.wait_text("시작 손패 확인", region=self.region("header"), timeout=40)
        before = self.opening_hand("before-shuffle")
        self.click_control("openingShuffle", "Replace all six cards with the once-per-preparation shuffle")
        self.wait_text("전체 손패 교체를 사용했습니다", region=self.region("openingNotice"), timeout=30)
        after = self.opening_hand("after-shuffle")
        self.check(before["cards"] != after["cards"], "Actual whole-hand shuffle changes the six visible card sequence",
            {"before":before["cards"], "after":after["cards"]})
        self.evidence("shuffle", {"before":before,"after":after,"method":"Real opening whole-hand replacement; every card identified from its rendered detail."})
        self.click_control("openingConfirm", "Confirm the shuffled six-card opening hand")
        self.wait_preparation()
        self.wait_text("손패 6", region=self.region("ownResources"), psm=7)
        self.evidence("starter_deck", {"selected":"숲과 빛 · 생명의 전열", "deckSize":50, "openingHand":6,
            "fourChoicesScreenshot":shot, "firstBattle":self.snapshot("classic-first-preparation")})

    def wait_preparation(self):
        self.page.wait_for_timeout(1100)
        return self.wait_text(["공격 준비", "방어 준비"], region=self.region("battleStatus"), timeout=70, psm=7)

    def observe(self, region, label, psm=7):
        pixels = self.capture(region)
        texts = []
        for language, upscale in [("kor+eng",False),("kor",False),("kor+eng",True)]:
            texts.append(self.text(pixels, psm=psm, language=language, upscale=upscale))
        index = len(self.report.setdefault("visibleObservations", [])) + 1
        path = self.output / f"visible-observation-{index:03}.png"
        path.write_bytes(pixels)
        entry = {"label":label,"region":region,"texts":texts,"rawPixels":path.name,
                 "sha256":hashlib.sha256(pixels).hexdigest()}
        self.report["visibleObservations"].append(entry)
        self.persist()
        return texts

    def resources(self):
        for attempt in range(4):
            texts = self.observe(self.region("ownResources"), "Own visible hand/deck/LP/terrain")
            values, sources = {}, {}
            # Each complete label+number must be recognized in the same raw
            # frame. Korean glyphs and Latin LP can favor different existing
            # OCR variants; preserve which full line supplied each field.
            for key,pattern,maximum in [("deck",r"덱\s*(\d+)\s*장",50),
                    ("hand",r"손패\s*(\d+)",6),("lp",r"LP\s*(\d+)",1000),
                    ("terrain",r"지형\s*(\d+)\s*/\s*10",10)]:
                for text in texts:
                    match = re.search(pattern,text)
                    if match and 0 <= int(match.group(1)) <= maximum:
                        values[key] = int(match.group(1)); sources[key] = text
                        break
            if "lp" not in values and "deck" in values:
                lp = self.lp_field(values["deck"])
                if lp is not None:
                    values["lp"] = lp[0]; sources["lp"] = lp[1]
            if len(values) == 4:
                values["ocr"] = sources
                return values
            self.page.wait_for_timeout(500)
        if self.args.pause_on_ocr_failure:
            index = len(self.report.setdefault("resourceOcrRecoveries",[]))+1
            resume = self.output / f"resource-ocr-resume-{index:03}.json"
            entry = {"status":"paused", "expectedFields":["덱 <integer>장","손패 <integer>","LP <integer>","지형 <integer>/10"],
                "originalRegion":self.region("ownResources"), "resumeFile":str(resume), "lastOcr":texts}
            self.report["resourceOcrRecoveries"].append(entry)
            entry["frame"]=self.snapshot(f"resource-ocr-paused-{index:03}"); self.persist()
            print(f"{GAME}: PAUSED resource OCR; only bounded ROI corrections accepted at {resume}",flush=True)
            deadline=time.monotonic()+1200
            while time.monotonic()<deadline:
                self.page.wait_for_timeout(1000)
                if not resume.exists(): continue
                recovery=json.loads(resume.read_text())
                if recovery.get("expectedFields")!=entry["expectedFields"]:
                    raise AssertionError("Resource recovery must preserve every label and numeric criterion")
                if recovery.get("mode")=="visual-confirmation":
                    values=dict(recovery.get("confirmedValues",{}))
                    valid=(set(values)=={"deck","hand","lp","terrain"} and
                        all(type(values.get(k)) is int and 0<=values[k]<=maximum for k,maximum in
                            [("deck",50),("hand",6),("lp",1000),("terrain",10)]))
                    if (not valid or recovery.get("reviewedFrameSha256")!=entry["frame"]["sha256"] or
                            not recovery.get("reviewer") or not recovery.get("reviewedUtc")):
                        raise AssertionError("Resource review requires bounded visible values and exact original frame provenance")
                    entry.update({"status":"confirmed-by-direct-original-frame-review","recovery":recovery})
                    self.report.setdefault("visualConfirmations",[]).append({**recovery,"frame":entry["frame"],
                        "originalOcr":texts,"method":"Direct original-frame numeric reading; not automatic OCR acceptance."})
                    values["visualConfirmation"]=recovery;self.persist();return values
                region=recovery.get("region")
                if not isinstance(region,list) or len(region)!=4 or not (0<=region[0]<region[2]<=1 and 0<=region[1]<region[3]<=1):
                    raise AssertionError("Resource recovery must provide a bounded visible text ROI")
                self.layout()["regions"]["ownResources"]=region
                entry.update({"status":"resumed-with-same-fields","recovery":recovery})
                self.persist()
                return self.resources()
        self.check(False, "Visible resource counts are legible", texts)

    def lp_field(self, deck_count):
        # LP 0 is often read as LPO by sentence OCR. Read this numeric field
        # independently, retaining its actual label and using an explicit
        # numeric alphabet. Never substitute O->0 in an OCR result string.
        region=self.region("ownResources")
        row_pixels=self.capture(region)
        row=BASE.Image.open(BASE.io.BytesIO(row_pixels)).convert("RGB")
        command=["tesseract","stdin","stdout"]+(["--tessdata-dir",str(self.args.tessdata)] if self.args.tessdata else [])
        tsv=subprocess.run(command+["-l","kor+eng","--psm","7","-c","tessedit_create_tsv=1"],
            input=row_pixels,capture_output=True,check=True).stdout.decode()
        words=[]
        for line in tsv.splitlines()[1:]:
            fields=line.split("\t",11)
            if len(fields)==12 and re.fullmatch(r"LP[0-9OIl]*",fields[11].strip()):
                words.append(fields)
        if len(words)!=1: return None
        word=words[0]
        left,top,width,height=map(int,word[6:10])
        box=(max(0,left-2),max(0,top-2),min(row.width,left+width+2),min(row.height,top+height+2))
        cropped=row.crop(box); original=BASE.io.BytesIO();cropped.save(original,format="PNG");raw=original.getvalue()
        source=cropped.convert("L")
        enlarged=source.resize((source.width*4,source.height*4),BASE.Image.Resampling.LANCZOS)
        buffer=BASE.io.BytesIO(); enlarged.save(buffer,format="PNG")
        result=subprocess.run(command+["-l","eng","--psm","7","-c","tessedit_char_whitelist=LP0123456789"],
            input=buffer.getvalue(),capture_output=True,check=True).stdout.decode().strip()
        index=len(self.report.setdefault("numericFieldObservations",[]))+1
        path=self.output/f"numeric-field-{index:03}.png";path.write_bytes(raw)
        evidence={"field":"LP", "region":region,"rawPixels":path.name,"sha256":hashlib.sha256(raw).hexdigest(),
            "locator":"Visible LP-prefixed word box from Korean+English OCR of the same resource row",
            "locatorWord":word[11],"wordBoxInResourcePixels":box,
            "preprocessing":"grayscale, 4x Lanczos; English PSM7; alphabet LP0123456789", "ocr":result}
        self.report["numericFieldObservations"].append(evidence);self.persist()
        match=re.fullmatch(r"LP\s*(\d+)",result)
        return (int(match.group(1)),evidence) if match and int(match.group(1))<=1000 else None

    def scroll_hand(self, side):
        if self.hand_scroll == side: return
        start, end = ((.94,.78),(.23,.78)) if side == "right" else ((.23,.78),(.94,.78))
        for _ in range(2):
            if self.args.profile == "mobile-landscape":
                self.touch_drag_hold(start, end, 300, "Scroll actual six-card hand " + side, reference=False)
            else:
                sx,sy,_,_ = self.canvas_point(*start); ex,ey,_,_ = self.canvas_point(*end)
                self.page.mouse.move(sx,sy); self.page.mouse.down(); self.page.mouse.move(ex,ey,steps=16)
                self.page.wait_for_timeout(300); self.page.mouse.up()
                self.report["input"].append({"kind":"mouse-drag", "label":"Scroll actual hand " + side,"from":start,"to":end})
            self.page.wait_for_timeout(350)
        self.hand_scroll = side
        self.persist()

    def hand_point(self, index, count):
        if count > 5:
            width = .16 if self.canvas_geometry["width"] < 1400 else .133
            side = "right" if index >= 5 else "left"
            self.scroll_hand(side)
            left = .98-count*width if side == "right" else .18
        else:
            width = min(.16,.79/max(1,count)); left = .59-count*width/2
        return left+(index+.5)*width-.004, .84

    def identify_hand(self, count):
        names = []
        for index in range(count):
            x,y = self.hand_point(index,count)
            if self.args.profile == "mobile-landscape":
                self.touch_drag_hold((x,y),(x,y),750,f"Long-press hand card {index+1} for its actual detail",reference=False)
                name = self.card_name(self.region("detailName"))
                self.click_control("detailBack", "Return from touch card detail")
                # The current UI preserves its visible hand scroll position
                # across detail/selection redraws. Only full reload resets it.
            else:
                px,py,_,_ = self.canvas_point(x,y)
                self.page.mouse.move(px,py); self.page.wait_for_timeout(550)
                tip_x = min(x+.018,.555)
                name = self.card_name((tip_x+.018,.405,tip_x+.415,.505))
                self.report["input"].append({"kind":"mouse-hover", "label":"Read actual hand tooltip", "card":name,"point":[x,y]})
                px,py,_,_ = self.canvas_point(.995,.5); self.page.mouse.move(px,py)
            names.append(name)
        self.report.setdefault("identifiedHands",[]).append({"cards":list(names),"method":"Rendered tooltip" if self.args.profile=="desktop" else "Rendered touch detail"})
        self.persist()
        return names

    def play_preparation(self, turn_index):
        before = self.resources()
        hand = self.identify_hand(before["hand"])
        # A legal six-card hand can contain no land. Merely completing turns
        # would retain all six cards, so use the authored whole-hand control on
        # a later preparation before playing anything. Opening replacement has
        # already consumed the first preparation's allowance.
        if (turn_index > 1 and before["lp"] > 0 and before["terrain"] < 3 and
                not any(int(self.by_name[name]["fields"]["Category"]) == 1 for name in hand)):
            old_hand = list(hand)
            self.click_control("handShuffle", "Use the legal preparation shuffle to seek missing terrain")
            self.wait_text("준비 완료",region=self.region("ready"),timeout=30,button_text=True)
            hand = self.identify_hand(self.resources()["hand"])
            self.check(old_hand != hand, "Preparation whole-hand control changes the visible card sequence",
                       {"before":old_hand,"after":hand})
            self.report["classicEvidence"]["shuffle"].setdefault("preparationShuffles",[]).append(
                {"preparation":turn_index,"before":old_hand,"after":hand,
                 "screenshot":self.snapshot(f"preparation-{turn_index}-whole-hand-shuffle")})
            self.persist()
        played = []
        # Only board labels decide legality. Static LV merely orders candidates.
        for kind in (1,2):
            candidates = sorted([name for name in hand if int(self.by_name[name]["fields"]["Category"]) == kind],
                key=lambda name:int(self.by_name[name]["fields"].get("LV",0)))
            for name in candidates:
                state = self.resources()
                if kind == 1 and (state["lp"] < 1 or state["terrain"] >= 10): continue
                index = hand.index(name)
                self.click(*self.hand_point(index,len(hand)), "Select visible " + name)
                target = None
                if kind == 1:
                    self.wait_text("지형 전개",region=(.61,.407,.774,.52),button_text=True)
                    self.click_control("cardAction", "Deploy actual terrain " + name)
                else:
                    for lane in range(5):
                        left = .025+lane*.19
                        texts = self.observe((left+.005,.602,left+.178,.668), "Legal placement label in lane " + str(lane),psm=7)
                        if any("여기에배치" in BASE.compact(t) for t in texts): target=lane; break
                    if target is None: continue
                    self.click(.116+target*.19,.6025, "Place actual unit " + name + " in lane " + str(target+1))
                self.wait_preparation()
                after = self.resources()
                self.check(after["hand"] == state["hand"]-1, "A real card play consumes one visible hand card",
                    {"card":name,"before":state,"after":after})
                hand.pop(index)
                if kind == 1:
                    self.check(after["terrain"] == state["terrain"]+1, "Played terrain increments the actual visible terrain count")
                else:
                    # Read the placed unit through its actual board detail.
                    # Its small over-art lane caption remains in the original
                    # screenshot, while the large detail proves exact identity.
                    self.click(.116+target*.19,.6025,"Inspect the unit actually placed on the board")
                    self.wait_text(name,region=self.region("detailName"),psm=7,whole_name=True)
                    self.snapshot(f"preparation-{turn_index}-placed-unit-detail-{len(played)+1}")
                    self.click_control("detailBack","Return from the placed unit's actual detail")
                played.append({"name":name,"kind":"terrain" if kind==1 else "unit","lane":target,"before":state,"after":after})
                self.snapshot(f"preparation-{turn_index}-play-{len(played)}")
        return {"preparation":turn_index,"initialResources":before,"played":played,"afterResources":self.resources()}

    def combat(self):
        preparations, combats = [], []
        saw_coin = saw_damage = False
        for number in range(1,13):
            phase = self.wait_preparation()
            prep = self.play_preparation(number)
            prep["visiblePreparationPhase"] = phase
            preparations.append(prep)
            self.click_control("ready", "Complete my preparation and allow the actual AI/automatic combat")
            self.wait_preparation()
            self.click_control("battleLog", "Read the actual battle record after automatic combat")
            self.wait_text("전투 기록",region=self.region("header"))
            texts = self.observe(self.region("battleLog"),"Actual automatic battle record",psm=6)
            compact = " ".join(BASE.compact(t) for t in texts)
            coin = "앞면" in compact or "뒷면" in compact
            damage = any(word in compact for word in ["직접공격","피해","생존반격"])
            saw_coin |= coin; saw_damage |= damage
            shot = self.snapshot(f"automatic-combat-{number}-record")
            combats.append({"preparation":number,"ocr":texts,"coinFaceVisible":coin,"damageVisible":damage,"screenshot":shot})
            self.click_control("back", "Return from the visible battle record")
            played = [p for prep in preparations for p in prep["played"]]
            if number >= 2 and saw_coin and saw_damage and all(any(p["kind"]==kind for p in played) for kind in ("terrain","unit")):
                break
        played = [p for prep in preparations for p in prep["played"]]
        self.check(all(any(p["kind"]==kind for p in played) for kind in ("terrain","unit")),
            "Real browser inputs deployed both terrain and unit", played)
        self.check(len(combats)>=2 and saw_coin and saw_damage, "Actual automatic rounds display coin faces and attack damage", combats)
        phases = " ".join(BASE.compact(p["visiblePreparationPhase"]) for p in preparations)
        self.check("공격준비" in phases and "방어준비" in phases,
            "Actual browser flow visits both my attack and defense preparation", phases)
        self.evidence("preparation",preparations)
        self.evidence("combat",combats)
        self.snapshot("classic-battle-after-automatic-rounds")

    def save_reload(self):
        before = self.resources()
        before_hand = self.identify_hand(before["hand"])
        self.click_control("save", "Save the actually played classic battle")
        self.wait_text("저장했습니다",region=self.region("battleStatus"),psm=7)
        saved = self.snapshot("classic-saved-battle")
        self.click_control("battleTitle", "Return to title through its actual control")
        self.wait_title(); self.page.wait_for_timeout(1800)
        self.report["storageBeforeReload"] = BASE.browser_storage(self.page)
        self.loaded(reload=True)
        self.click_control("titleContinue", "Continue the classic save after a full page reload")
        self.audio_after_reload()
        self.wait_text("복원했습니다",region=self.region("battleStatus"),psm=7)
        after = self.resources()
        self.hand_scroll = "left"
        after_hand = self.identify_hand(after["hand"])
        self.check(all(before[k]==after[k] for k in ("deck","hand","lp","terrain")) and before_hand==after_hand,
            "Visible deck, hand, LP, terrain and every hand card survived full browser reload and Continue",
            {"before":before,"after":after,"beforeHand":before_hand,"afterHand":after_hand})
        self.report["checks"][-1]["id"]=NETWORK.RESTORE_CHECK_ID
        self.report["storageAfterReload"] = BASE.browser_storage(self.page)
        markers = self.report["storageAfterReload"]["saveMarkers"]
        self.check(any("fantasy-battle-classic-v3" in key for key in markers), "Browser persistence retains the new classic slot",markers)
        self.evidence("save_reload",{"before":before,"after":after,"beforeHand":before_hand,"afterHand":after_hand,
            "saved":saved,"restored":self.snapshot("classic-restored-battle"),"saveMarkers":markers})

    def fantasy(self):
        self.collection()
        self.start_battle()
        self.combat()
        self.save_reload()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-root",type=Path,default=ROOT/"Artifacts/Builds")
    parser.add_argument("--output",type=Path,required=True)
    parser.add_argument("--base-url")
    parser.add_argument("--revision",default="fm-classic-2026-10-26")
    parser.add_argument("--renderer",choices=["x11-mesa","swiftshader"],default="x11-mesa")
    parser.add_argument("--chromium",default=os.environ.get("CHROMIUM_BINARY","/usr/bin/chromium"))
    parser.add_argument("--tessdata",type=Path,default=os.environ.get("FIRSTGAME_TESSDATA"))
    parser.add_argument("--layouts",type=Path,default=Path(__file__).with_name("classic-cards-layouts.json"))
    parser.add_argument("--profile",choices=["desktop","mobile-landscape"],default="desktop")
    parser.add_argument("--timeout",type=int,default=180)
    parser.add_argument("--ocr-timeout",type=int,default=12)
    parser.add_argument("--pause-on-ocr-failure",action="store_true")
    args=parser.parse_args()
    args.verify_audio=True
    args.interaction_resolution="844x390" if args.profile=="mobile-landscape" else "1280x720"
    if not (args.build_root/GAME/"Web/index.html").is_file():
        parser.error("The final local classic Web build is required, including for public verification.")
    layout=json.loads(args.layouts.read_text())["games"][GAME]
    for variant in layout["variants"].values():
        if any(len(p)!=2 or any(v<0 or v>1 for v in p) for p in variant["points"].values()):
            parser.error("Every classic control must use bounded normalized canvas coordinates.")
    command=["tesseract","--list-langs"]+(["--tessdata-dir",str(args.tessdata)] if args.tessdata else [])
    if not {"kor","eng"}.issubset(set(subprocess.run(command,capture_output=True,text=True,check=True).stdout.splitlines())):
        parser.error("Korean and English Tesseract data are required.")
    if args.base_url: BASE.public_game_url(args.base_url,GAME)
    out=args.output/GAME
    previous=BASE.archive_previous_run(out)
    server=None
    if not args.base_url:
        server=http.server.ThreadingHTTPServer(("127.0.0.1",0),functools.partial(BASE.LocalHandler,directory=str(args.build_root.resolve())))
        threading.Thread(target=server.serve_forever,daemon=True).start()
    try:
        with BASE.sync_playwright() as playwright:
            renderer=(["--ozone-platform=x11","--use-gl=angle","--use-angle=gl","--ignore-gpu-blocklist"]
                if args.renderer=="x11-mesa" else ["--use-gl=angle","--use-angle=swiftshader","--enable-unsafe-swiftshader"])
            browser=playwright.chromium.launch(executable_path=args.chromium,headless=args.renderer!="x11-mesa",
                ignore_default_args=["--mute-audio"],args=["--no-sandbox","--disable-dev-shm-usage",*renderer])
            try:
                mobile=args.profile=="mobile-landscape"
                w,h=map(int,args.interaction_resolution.split("x"))
                options={"viewport":{"width":w,"height":h},"device_scale_factor":1,"has_touch":mobile,"is_mobile":mobile}
                if mobile:
                    options["user_agent"]="Mozilla/5.0 (Linux; Android 14; Mobile Browser Smoke) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36"
                context=browser.new_context(**options)
                try:
                    smoke=ClassicSmoke(context.new_page(),out,args)
                    if previous: smoke.report["previousRunArchive"]=str(previous)
                    url=BASE.public_game_url(args.base_url,GAME) if args.base_url else f"http://127.0.0.1:{server.server_port}/{GAME}/Web/"
                    report=smoke.run(url)
                finally: context.close()
            finally: browser.close()
    finally:
        if server: server.shutdown(); server.server_close()
    summary={"createdUtc":datetime.now(timezone.utc).isoformat(),"revision":args.revision,"profile":args.profile,
        "interactionResolution":args.interaction_resolution,"baseUrl":args.base_url,"status":report["status"],
        "visualReview":report["visualReview"],"games":[report],"limits":["Software browser, not physical mobile hardware.",
            "Browser scope is several actual rounds; full encounter wins and all card effects belong to separate native/core checks.",
            "Direct visual review must reference this run's exact original screenshots and five runtime hashes."]}
    summary_path=args.output/"summary.json"
    if summary_path.exists(): summary_path.rename(args.output/("summary.previous-"+datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")+".json"))
    summary_path.write_text(json.dumps(summary,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    return 0 if report["status"]=="passed" else 1


if __name__=="__main__": sys.exit(main())
