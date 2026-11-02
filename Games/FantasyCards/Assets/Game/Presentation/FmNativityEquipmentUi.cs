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
        private void ShowNativityUnitEquipment(int owner, int instanceId)
        {
            if (locked || engine == null || owner < 0 || owner > 1) return;
            var unit = engine.State.players[owner].units.Find(value => value.instanceId == instanceId);
            if (unit == null) { ShowBattle(); return; }
            QuestCurrentScreen = "unit-equipment";
            battleScreen = false; redraw = () => ShowNativityUnitEquipment(owner, instanceId);
            Clear(); ResetPolishTap(); polishBack = ShowBattle; Backdrop(true);
            var definition = CardCatalog.Get(unit.definitionId);
            ReadableText(root, (owner == 0 ? "내 유닛" : "상대 공개 유닛") + " · " + definition.name,
                .035f, .855f, .965f, .965f, 34, owner == 0 ? teal : gold);
            var face = ArcanaCardFace(root, definition, .035f, .21f, .365f, .845f, ArcanaCardMode.Field, false, unit);
            face.name = "Equipped bearer " + instanceId;
            ReadableText(root, "현재 AP " + CardCatalog.UnitAttack(unit) + " / DP " + CardCatalog.UnitDefense(unit) +
                " / HP " + CardCatalog.UnitHealth(unit), .035f, .155f, .365f, .21f, 22, ivory, TextAlignmentOptions.Center);
            ReadableText(root, "장착 아이템 " + unit.equipment.Count + " / 2", .405f, .76f, .965f, .85f, 27, gold);
            for (int i = 0; i < unit.equipment.Count; i++)
            {
                var item = unit.equipment[i]; var card = CardCatalog.Get(item.definitionId);
                float left = .405f + i * .285f;
                var original = ArcanaCardFace(root, card, left, .305f, left + .265f, .75f, ArcanaCardMode.Detail);
                original.name = "Attached original item " + item.instanceId;
                original.GetComponent<Image>().raycastTarget = true;
                var detail = original.gameObject.AddComponent<Button>(); detail.transition = Selectable.Transition.None;
                detail.onClick.AddListener(() => ShowDetail(card, () => ShowNativityUnitEquipment(owner, instanceId)));
                BindCardHelp(original.gameObject, card, () => ShowDetail(card, () => ShowNativityUnitEquipment(owner, instanceId)));
                ReadableText(root, card.name, left, .245f, left + .265f, .305f, 22, ivory, TextAlignmentOptions.Center);
                string reason = owner == 0 ? "" : "상대의 공개 장비는 확인만 할 수 있습니다.";
                bool allowed = owner == 0 && engine.CanDiscardField(0, item.instanceId, out reason);
                float height = Mathf.Max(.095f, 44f / Mathf.Max(1, canvas.pixelRect.height));
                var discard = Button(root, "장비 소각", left, .10f, left + .265f, .10f + height,
                    () => DiscardSelectedField(item.instanceId), allowed ? gold : slate, 23, allowed);
                discard.name = "Discard attached item " + item.instanceId;
                BindTooltip(discard.gameObject, "장비 소각", allowed
                    ? "실제 장비 1장을 소각장으로 보냅니다. 비용은 반환되지 않으며, HP 장비가 사라져 유닛도 파괴될 수 있습니다."
                    : reason);
            }
            if (unit.equipment.Count > 0)
            {
                string status = "HP 장비를 소각하면 유닛도 파괴될 수 있습니다.";
                if (owner == 1) status = "상대의 공개 장비는 확인만 할 수 있습니다.";
                else if (!engine.CanDiscardField(0, unit.equipment[0].instanceId, out var reason)) status = reason;
                ReadableText(root, status, .405f, .02f, .965f, .09f, 18, teal, TextAlignmentOptions.Center);
            }
            if (unit.equipment.Count == 0)
                ReadableText(root, "장착한 아이템이 없습니다.", .405f, .39f, .965f, .65f, 28, ivory, TextAlignmentOptions.Center);
            var back = Button(root, "전장으로", .035f, .025f, .19f, .14f, ShowBattle, gold, 24);
            back.name = "Equipment inspection back";
            Button(root, "원본 상세", .205f, .025f, .365f, .14f,
                () => ShowDetail(definition, () => ShowNativityUnitEquipment(owner, instanceId)), slate, 22);
        }
    }
}
