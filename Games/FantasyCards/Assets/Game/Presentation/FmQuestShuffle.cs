using System.Collections;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        public bool IsClientShuffleActive { get; private set; }

        // Official tutorial2_1: all six hand cards return to the deck, are mixed,
        // then are dealt in order. The Core has already resolved that operation;
        // this on-field display never consumes RNG or invents a new hand.
        private IEnumerator AnimateClientShuffle(bool opening, bool returnExistingHand)
        {
            locked = true; IsClientShuffleActive = true;
            ShowBattle(); redraw = null; QuestCurrentScreen = "shuffle";
            var layer = Panel(root, 0, 0, 1, 1, Color.clear);
            layer.gameObject.name = "Original hand-to-deck shuffle presentation";
            layer.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            Panel(layer, .025f, .012f, .805f, .277f, new Color(.008f, .017f, .035f, .99f));
            var caption = Text(layer, "SHUFFLE", .816f, .31f, .978f, .37f, 23, gold, TextAlignmentOptions.Center);
            caption.gameObject.name = "Official full-hand shuffle caption";
            var hand = engine.State.players[0].hand;
            int count = hand.Count;
            var views = new RectTransform[count]; var faced = new bool[count];
            var deck = new Vector2(.895f, .40f);
            Vector2 Hand(int i) => new Vector2(.025f + .78f * (i + .5f) / Mathf.Max(1, count), .145f);
            void Place(RectTransform view, Vector2 center, float scale, float angle = 0)
            {
                Anchor(view, center.x - .052f, center.y - .116f, center.x + .052f, center.y + .116f);
                view.localScale = Vector3.one * scale; view.localRotation = Quaternion.Euler(0, 0, angle);
            }
            for (int i = 0; i < count; i++)
            {
                views[i] = Panel(layer, 0, 0, 0, 0, Color.clear);
                views[i].gameObject.name = "Hand shuffle original back " + i;
                ArcanaHiddenBack(views[i], 0, 0, 1, 1);
                Place(views[i], returnExistingHand ? Hand(i) : deck, returnExistingHand ? 1 : .32f);
            }
            FmBattleAudio.Play("card-shuffle");
            const float duration = .94f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    if (returnExistingHand && elapsed < .31f)
                    {
                        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - i * .015f) / .22f));
                        Place(views[i], Vector2.Lerp(Hand(i), deck, t), Mathf.Lerp(1, .32f, t));
                    }
                    else if (elapsed < .40f)
                    {
                        float flutter = Mathf.Sin(elapsed * 54 + i * 1.3f);
                        Place(views[i], deck + new Vector2(flutter * .009f, i * .002f), .32f, flutter * 5);
                    }
                    else
                    {
                        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .40f - i * .055f) / .20f));
                        Place(views[i], Vector2.Lerp(deck, Hand(i), t), Mathf.Lerp(.32f, 1, t));
                        if (t >= .999f && !faced[i])
                        {
                            faced[i] = true; FmBattleAudio.Play("card-draw");
                            foreach (Transform child in views[i]) child.gameObject.SetActive(false);
                            var definition = CardCatalog.Get(hand[i].definitionId);
                            ArcanaCardFace(views[i], definition, 0, 0, 1, 1, ArcanaCardMode.Hand);
                        }
                    }
                }
                yield return null;
            }
            IsClientShuffleActive = false; locked = false;
            if (opening) ShowOpeningHand(); else ShowBattle();
        }
    }
}
