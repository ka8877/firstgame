using FantasyCards.Core;
using UnityEngine;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private void BindHandDrag(GameObject view, int instanceId, CardDefinition definition)
        {
            var drag = view.GetComponent<FmCardDragDrop>();
            if (drag == null) drag = view.AddComponent<FmCardDragDrop>();
            drag.Configure(instanceId,
                () => !locked && battleScreen && engine != null && engine.IsPreparing(0) && !engine.State.openingHandOpen,
                () =>
                {
                    ResetPolishTap();
                    selectedCard = instanceId; inspectedId = definition.id; HideTooltip();
                    message = definition.kind == CardKind.Terrain ? "지형 카드를 아래 지형·소울 띠에 놓으세요." : "빛나는 전열 또는 대상 유닛에 놓으세요.";
                },
                () => { if (!locked && battleScreen && engine != null) ShowBattle(); });
        }

        private bool CanDropCard(int instanceId, int owner, int lane, bool terrain)
        {
            if (locked || !battleScreen || engine == null || !engine.IsPreparing(0)) return false;
            var instance = engine.State.players[0].hand.Find(card => card.instanceId == instanceId);
            if (instance == null) return false;
            var card = CardCatalog.Get(instance.definitionId);
            if (terrain) return card.kind == CardKind.Terrain && engine.CanPlay(0, instanceId, -1, out _);
            if (card.kind == CardKind.Terrain || owner != (TargetsEnemy(card) ? 1 : 0)) return false;
            if (card.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(card))
                return engine.CanPlayInSlot(0, instanceId, lane, -1, out _);
            return engine.CanPlay(0, instanceId, lane, out _);
        }

        private void BindLaneDrop(RectTransform view, int owner, int lane)
        {
            var target = view.gameObject.AddComponent<FmCardDropTarget>();
            target.Configure(id => CanDropCard(id, owner, lane, false), id =>
            {
                selectedCard = id;
                var instance = engine.State.players[0].hand.Find(card => card.instanceId == id);
                if (instance == null) return;
                var definition = CardCatalog.Get(instance.definitionId);
                if (definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition))
                    Act(() => engine.PlayInSlot(0, id, lane, -1, out message));
                else Play(lane);
            });
        }

        private void BindTerrainDrop(RectTransform view)
        {
            var target = view.gameObject.AddComponent<FmCardDropTarget>();
            target.Configure(id => CanDropCard(id, 0, -1, true), id => { selectedCard = id; Play(-1); });
        }

        private void BindDiscardDrop(RectTransform view)
        {
            var target = view.gameObject.AddComponent<FmCardDropTarget>();
            target.Configure(id => !locked && battleScreen && engine != null && engine.CanDiscardHand(0, id, out _),
                id => { selectedCard = id; Act(() => engine.DiscardHand(0, id, out message)); });
        }
    }
}
