using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        void AddTerrainProduction(PlayerState player, CardInstance instance)
        {
            var terrain = CardCatalog.Get(instance.definitionId);
            int colored = player.Resource(terrain.element), total = player.genericSoul;
            AddProduction(player, terrain);
            instance.hasSoulProductionRecord = true; instance.terrainSoulUsed = false;
            // Only production actually admitted below the resource cap belongs
            // to this card. Disposing of capped production cannot take other
            // terrain's or an upgrade unit's resources away.
            instance.remainingColoredProduction = player.Resource(terrain.element) - colored;
            instance.remainingGenericProduction = player.genericSoul - total;
        }

        void SpendSouls(PlayerState player, Element element, int coloredCost, int totalCost)
        {
            // The source defines spent terrain, but not which identical land
            // pays first. This reconstruction spends non-terrain/unknown pools
            // first, then matching terrain in field order; the same terrain's
            // total production pays the colored charge when available.
            var known = player.terrain.Where(c => c.hasSoulProductionRecord).ToArray();
            var matching = known.Where(c => CardCatalog.Get(c.definitionId).element == element).ToArray();
            int coloredFromTerrain = Math.Max(0, coloredCost - Math.Max(0,
                player.Resource(element) - matching.Sum(c => c.remainingColoredProduction)));
            var used = new List<CardInstance>();
            foreach (var terrain in matching)
            {
                int amount = Math.Min(coloredFromTerrain, terrain.remainingColoredProduction);
                if (amount == 0) continue;
                terrain.remainingColoredProduction -= amount; terrain.terrainSoulUsed = true;
                coloredFromTerrain -= amount; used.Add(terrain);
            }
            int totalFromTerrain = Math.Max(0, totalCost - Math.Max(0,
                player.genericSoul - known.Sum(c => c.remainingGenericProduction)));
            foreach (var terrain in used.Concat(known.Where(c => !used.Contains(c))))
            {
                int amount = Math.Min(totalFromTerrain, terrain.remainingGenericProduction);
                if (amount == 0) continue;
                terrain.remainingGenericProduction -= amount; terrain.terrainSoulUsed = true;
                totalFromTerrain -= amount;
            }
            player.SetResource(element, player.Resource(element) - coloredCost);
            player.genericSoul -= totalCost;
        }

        bool CanDiscardTerrain(PlayerState player, CardInstance terrain, out string reason)
        {
            if (!terrain.hasSoulProductionRecord)
                return Fail("이전 저장의 지형 사용 기록은 다음 소울 회복부터 확인할 수 있습니다.", out reason);
            if (terrain.terrainSoulUsed) return Fail("이번 소울 회복 후 소울을 사용한 지형은 소각할 수 없습니다.", out reason);
            reason = ""; return true;
        }

        void DiscardTerrain(int owner, CardInstance terrain)
        {
            var player = State.players[owner]; var definition = CardCatalog.Get(terrain.definitionId);
            player.SetResource(definition.element, player.Resource(definition.element) - terrain.remainingColoredProduction);
            player.genericSoul -= terrain.remainingGenericProduction;
            player.terrain.Remove(terrain); player.discard.Add(terrain);
            // No LP refund, no replacement production, no RNG draw.
            Log("discard-field", definition.name + " · 미사용 지형 소각", 1, owner, card: terrain, sourceOwner: owner);
        }

        static void ValidateTerrainProduction(PlayerState player)
        {
            foreach (var terrain in player.terrain)
            {
                var definition = CardCatalog.Get(terrain.definitionId);
                if (terrain.remainingColoredProduction < 0 || terrain.remainingColoredProduction > definition.producedColored ||
                    terrain.remainingGenericProduction < 0 || terrain.remainingGenericProduction > definition.producedGeneric ||
                    (!terrain.hasSoulProductionRecord && (terrain.terrainSoulUsed || terrain.remainingColoredProduction != 0 || terrain.remainingGenericProduction != 0)))
                    throw new ArgumentException("지형별 소울 사용 기록이 잘못되었습니다.");
            }
            if (player.terrain.Where(c => c.hasSoulProductionRecord).Sum(c => c.remainingGenericProduction) > player.genericSoul ||
                Enum.GetValues(typeof(Element)).Cast<Element>().Any(element => player.terrain.Where(c => c.hasSoulProductionRecord &&
                    CardCatalog.Get(c.definitionId).element == element).Sum(c => c.remainingColoredProduction) > player.Resource(element)))
                throw new ArgumentException("지형별 미사용 생산량이 남은 소울을 초과합니다.");
        }
    }
}
