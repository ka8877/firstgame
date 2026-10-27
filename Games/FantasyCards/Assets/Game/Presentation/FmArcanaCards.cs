using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private enum ArcanaCardMode { Hand, Album, Detail, Field, Duel, Showcase }
        private const float ArcanaCardAspect = 5f / 7f;

        // Slot hit areas can be wide for touch/drag. The visible card inside is
        // always an independently fitted portrait, never stretched to the slot.
        private RectTransform ArcanaCardFace(Transform parent, CardDefinition card,
            float x0, float y0, float x1, float y1, ArcanaCardMode mode,
            bool selected = false, UnitState live = null)
        {
            var bounds = Panel(parent, x0, y0, x1, y1, Color.clear);
            bounds.gameObject.name = "Card " + card.id;
            var surface = Panel(bounds, 0, 0, 1, 1, Color.Lerp(new Color(.035f, .045f, .085f), RarityColor(card.rarity), .15f));
            surface.gameObject.name = "Arcana portrait surface " + card.id;
            var fit = surface.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = ArcanaCardAspect;
            var descriptor = surface.gameObject.AddComponent<FmArcanaCardSurface>();
            descriptor.DefinitionId = card.id; descriptor.Grade = card.rarity;
            var baseShape = surface.GetComponent<UiRoundedImage>(); baseShape.Radius = 20;
            baseShape.TopTint = new Color(1.25f, 1.25f, 1.35f, 1);
            baseShape.BottomTint = new Color(.65f, .68f, .83f, 1);
            baseShape.BorderColor = selected ? new Color(1, .94f, .68f) : RarityColor(card.rarity);
            baseShape.BorderWidth = selected ? 5 : 3.4f;
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var inner = Panel(surface, .025f, .018f, .975f, .982f, new Color(.035f, .045f, .075f));
            inner.GetComponent<UiRoundedImage>().Radius = 15;
            Frame(inner, Color.Lerp(RarityColor(card.rarity), ivory, .30f), 1.5f);
            // Engraved inner line and a coloured foil shoulder are distinct from
            // the attribute gem. The grade is a classification, not power rank.
            Gradient(surface, .045f, .735f, .955f, .97f,
                new Color(.035f, .05f, .09f), Color.Lerp(new Color(.09f, .12f, .19f), RarityColor(card.rarity), .32f));
            Panel(surface, .045f, .974f, .955f, .982f, Color.Lerp(RarityColor(card.rarity), ivory, .35f));
            Panel(surface, .07f, .018f, .93f, .025f, RarityColor(card.rarity));

            bool field = mode == ArcanaCardMode.Field;
            bool detail = mode == ArcanaCardMode.Detail || mode == ArcanaCardMode.Showcase;
            bool album = mode == ArcanaCardMode.Album;
            bool duel = mode == ArcanaCardMode.Duel;
            bool small = canvas.pixelRect.height < 600;
            bool compactStats = small && !field && !detail && !duel;
            float artBottom = field ? .10f : detail ? .425f : compactStats ? .405f : album ? .365f : duel ? .25f : .365f;
            float artTop = field ? .925f : detail ? .84f : album ? (small ? .77f : .80f) : duel ? (small ? .67f : .80f) : .77f;
            var artFrame = Panel(surface, .050f, artBottom, .950f, artTop, new Color(.018f, .025f, .045f));
            artFrame.gameObject.name = "Arcana art window";
            artFrame.GetComponent<UiRoundedImage>().Radius = 9;
            artFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            ArcanaArtwork(artFrame, card);
            Frame(artFrame, Color.Lerp(RarityColor(card.rarity), ivory, .38f), 1.5f);
            if (field)
            {
                Gradient(surface, .04f, .76f, .96f, .98f, Color.clear, new Color(.015f, .025f, .055f, .9f));
                Gradient(surface, .04f, .02f, .96f, .24f, new Color(.015f, .025f, .055f, .96f), Color.clear);
                return bounds;
            }
            float nameBottom = detail ? .855f : album ? (small ? .765f : .812f) : duel ? (small ? .66f : .79f) : .765f;
            var name = Text(surface, card.name, .245f, nameBottom, .95f, .977f,
                detail ? 33 : album ? 26 : 23, ivory, TextAlignmentOptions.Center);
            name.gameObject.name = "Arcana card name";
            name.textWrappingMode = TextWrappingModes.Normal; name.lineSpacing = -7;
            name.overflowMode = TextOverflowModes.Ellipsis;
            ArcanaCostGem(surface, card, .018f, detail ? .825f : .77f, .24f, .997f, false);
            ArcanaGradeSeal(surface, card, .60f, artBottom + .007f, .94f, artBottom + (detail ? .07f : duel && small ? .18f : .115f), !detail);
            string kind = CardCatalog.KindName(card.kind) + (card.kind == CardKind.Unit ? " · " + card.rank : "");
            if (detail)
            {
                ArcanaText(surface, kind, .065f, detail ? .365f : .31f, .58f, detail ? .42f : .36f, detail ? 22 : 20, ElementColor(card.element), "Arcana type");
            }
            float costBottom = detail ? .295f : compactStats ? .265f : album && !small ? .29f : .225f;
            float costTop = detail ? .355f : compactStats ? .395f : .355f;
            var soul = duel ? null : Panel(surface, .055f, costBottom, .945f, costTop, new Color(.085f, .105f, .16f, .96f));
            string costLine = card.kind == CardKind.Terrain
                ? "Lv" + card.level + " · LP" + card.landPointCost + " → " + CardCatalog.ElementName(card.element) + card.producedColored + "/총" + card.producedGeneric
                : "Lv" + card.level + " · " + CardCatalog.ElementName(card.element) + card.coloredCost + "/총" + card.cost;
            // Production already has its own plate. A compact row must not
            // repeat it and truncate the actual payment at the 12 px floor.
            if (small && !detail)
                costLine = card.kind == CardKind.Terrain ? "Lv" + card.level + " · LP" + card.landPointCost
                    : "L" + card.level + " " + CardCatalog.ElementName(card.element).Substring(0, 1) + card.coloredCost + "/총" + card.cost;
            if (soul != null) ArcanaText(soul, costLine, .02f, .02f, .98f, .98f, detail ? 25 : 20, gold, "Mini cost");
            if (detail)
            {
                var paper = Panel(surface, .055f, .165f, .945f, .285f, new Color(.89f, .89f, .86f));
                paper.GetComponent<UiRoundedImage>().Radius = 6;
                string words = CardAbilitySummary(card) + "\n" + card.text;
                var rules = Text(paper, words, .045f, .055f, .955f, .945f, 21, new Color(.10f, .15f, .22f), TextAlignmentOptions.TopLeft);
                rules.gameObject.name = "Arcana printed rules"; rules.overflowMode = TextOverflowModes.Ellipsis;
            }
            else if (album && !small)
            {
                var ribbon = Panel(surface, .055f, .185f, .945f, .282f, new Color(.075f, .09f, .14f));
                var summary = ArcanaText(ribbon, CardAbilitySummary(card).Replace("유닛 능력 · ", "").Replace("사용 시점 · ", ""),
                    .045f, .025f, .955f, .975f, 20, new Color(.75f, .81f, .91f), "Arcana ability ribbon");
                // Prose is an excerpt. Numeric cells retain their exact-value
                // fit, while this line keeps its beginning and ends cleanly.
                summary.alignment = TextAlignmentOptions.MidlineLeft;
                summary.overflowMode = TextOverflowModes.Ellipsis;
            }
            float statsTop = detail ? .155f : compactStats ? .26f : album && !small ? .18f : duel ? .23f : .225f;
            ArcanaCardStats(surface, card, live, .045f, .045f, .955f, statsTop, detail || album || canvas.pixelRect.height >= 600, compactStats);
            return bounds;
        }

        private TMP_Text ArcanaText(Transform parent, string text, float x0, float y0, float x1, float y1,
            float size, Color tint, string role)
        {
            var label = MiniCardText(parent, text, x0, y0, x1, y1, size, tint, role);
            label.characterWidthAdjustment = 0;
            return label;
        }

        private void ArcanaArtwork(RectTransform artFrame, CardDefinition card)
        {
            var texture = OriginalCardTexture(card);
            if (texture != null && card.artResource.StartsWith("Art/ClassicCards/", StringComparison.Ordinal))
                ImageFrame(artFrame, texture, new Rect(.13f, .36f, .805f, .56f), .012f, .012f, .988f, .988f,
                    new Vector2(.5f, card.kind == CardKind.Unit ? .69f : .54f));
            else Artwork(artFrame, card, .012f, .012f, .988f, .988f);
        }

        private void ArcanaCostGem(RectTransform parent, CardDefinition card, float x0, float y0, float x1, float y1, bool compact)
        {
            var bounds = new GameObject("Arcana cost gem bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            bounds.SetParent(parent, false); Anchor(bounds, x0, y0, x1, y1);
            var gem = Panel(bounds, 0, 0, 1, 1, Color.Lerp(new Color(.045f, .075f, .15f), ElementColor(card.element), .40f));
            gem.gameObject.name = "Arcana cost gem";
            var shape = gem.GetComponent<UiRoundedImage>(); shape.Radius = 1000; shape.BorderWidth = 2.8f;
            shape.BorderColor = Color.Lerp(ElementColor(card.element), Color.white, .45f);
            shape.TopTint = new Color(1.4f, 1.5f, 1.7f); shape.BottomTint = new Color(.4f, .45f, .6f);
            var fitter = gem.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 1;
            string value = (card.kind == CardKind.Terrain ? card.landPointCost : card.cost).ToString();
            bool single = compact || canvas.pixelRect.height < 600;
            if (!single) ArcanaText(gem, card.kind == CardKind.Terrain ? "LP" : "총", .14f, .59f, .86f, .94f, 16, ivory, "Arcana cost label");
            var number = ArcanaText(gem, value, .10f, single ? .06f : .035f, .90f, single ? .94f : .66f,
                compact ? 22 : 27, Color.white, "Arcana total cost"); number.fontStyle = FontStyles.Bold;
        }

        private void ArcanaGradeSeal(RectTransform parent, CardDefinition card, float x0, float y0, float x1, float y1, bool codeOnly, bool micro = false)
        {
            Color grade = RarityColor(card.rarity);
            var seal = Panel(parent, x0, y0, x1, y1, new Color(.025f, .035f, .070f, .97f));
            seal.gameObject.name = "Card grade badge " + CardCatalog.RarityCode(card.rarity);
            seal.GetComponent<UiRoundedImage>().Radius = 7;
            Frame(seal, grade, 2.5f);
            if (micro) return; // Field colour seal; readable code is in the external lane caption.
            string value = codeOnly || canvas.pixelRect.height < 600 ? CardCatalog.RarityCode(card.rarity)
                : CardCatalog.RarityCode(card.rarity) + " · " + CardCatalog.RarityName(card.rarity);
            ArcanaText(seal, value, .025f, .025f, .975f, .975f, 18, grade, "Card grade code");
        }

        private void ArcanaCardStats(RectTransform parent, CardDefinition card, UnitState live,
            float x0, float y0, float x1, float y1, bool labels, bool compactRows)
        {
            var strip = Panel(parent, x0, y0, x1, y1, new Color(.045f, .065f, .10f));
            strip.gameObject.name = "Arcana stats plate";
            labels = labels && strip.rect.height * canvas.scaleFactor >= 31;
            if (card.kind != CardKind.Unit && card.kind != CardKind.Equipment)
            {
                string text = card.kind == CardKind.Terrain ? (canvas.pixelRect.height < 600 ? "" : "생산 ") + CardCatalog.ElementName(card.element) + card.producedColored + " / 총" + card.producedGeneric
                    : CardAbilitySummary(card).Replace("사용 시점 · ", "");
                ArcanaText(strip, text, .025f, .02f, .975f, .98f, 20, ElementColor(card.element), "Mini kind");
                return;
            }
            string[] titles = { "AP", "DP", "HP" };
            string[] values = live == null ? new[] { card.attack + CardCatalog.CoinText(card.attackCoin), card.defense + CardCatalog.CoinText(card.defenseCoin), card.health + CardCatalog.CoinText(card.healthCoin) }
                : new[] { CardCatalog.UnitAttack(live).ToString(), CardCatalog.UnitDefense(live).ToString(), Math.Max(0, CardCatalog.UnitHealth(live)).ToString() };
            if (compactRows)
            {
                // Three full coin expressions cannot fit 12px type in three
                // narrow portrait columns. Keep each expression unabridged.
                float split = Mathf.Clamp(values[0].Length / (float)Mathf.Max(1, values[0].Length + values[1].Length), .30f, .70f);
                ArcanaText(strip, values[0], .015f, .51f, split - .01f, .99f, 23, ivory, "Mini AP");
                ArcanaText(strip, values[1], split + .01f, .51f, .985f, .99f, 23, ivory, "Mini DP");
                ArcanaText(strip, values[2], .015f, .01f, .985f, .49f, 23, live != null && live.damage > 0 ? new Color(1, .61f, .51f) : new Color(.47f, .94f, .73f), "Mini HP");
                Panel(strip, .025f, .495f, .975f, .505f, new Color(.42f, .49f, .65f, .28f));
                return;
            }
            float[] weights = values.Select(value => (float)Mathf.Max(3, value.Length)).ToArray();
            float total = weights.Sum(), cursor = 0;
            for (int i = 0; i < 3; i++)
            {
                float left = cursor / total; cursor += weights[i]; float right = cursor / total;
                if (i > 0) Panel(strip, left - .002f, .1f, left + .002f, .9f, new Color(.42f, .49f, .65f, .28f));
                if (labels) ArcanaText(strip, titles[i], left, .54f, right, .98f, 18, new Color(.68f, .77f, .91f), "Mini label " + titles[i]);
                var value = ArcanaText(strip, values[i], left + .005f, .02f, right - .005f, labels ? .55f : .98f,
                    23, i == 2 ? new Color(.47f, .94f, .73f) : ivory, "Mini " + titles[i]);
                if (live != null && i == 2 && live.damage > 0) value.color = new Color(1, .61f, .51f);
            }
        }

        private void DrawArcanaCard(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1,
            bool selected, Action onClick, bool interactable)
        {
            var view = ArcanaCardFace(parent, card, x0, y0, x1, y1,
                battleScreen ? ArcanaCardMode.Hand : ArcanaCardMode.Album, selected);
            view.GetComponent<Image>().raycastTarget = true;
            var surface = view.GetComponentInChildren<FmArcanaCardSurface>().GetComponent<Image>();
            var button = view.gameObject.AddComponent<Button>(); button.targetGraphic = surface; button.interactable = interactable;
            var colours = button.colors; colours.fadeDuration = .15f; colours.highlightedColor = new Color(1.09f, 1.09f, 1.14f);
            colours.pressedColor = new Color(.88f, .91f, 1); button.colors = colours;
            button.onClick.AddListener(() => onClick());
            BindCardHelp(view.gameObject, card, () => ShowDetail(card, redraw));
            var hover = view.GetComponent<UiCardHover>(); hover.HoverScale = 1.045f; hover.HoverLift = 7;
        }
    }
}
