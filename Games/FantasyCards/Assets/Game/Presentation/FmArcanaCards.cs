using System;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private enum ArcanaCardMode { Hand, Album, Detail, Field, Duel, Showcase }
        private const float ArcanaCardAspect = 284f / 404f;

        // The source is a complete printed card, not an illustration atlas.
        // Every mode displays all source pixels at the source aspect ratio.
        // Names, changing battle values and help belong outside this surface.
        private RectTransform ArcanaCardFace(Transform parent, CardDefinition card,
            float x0, float y0, float x1, float y1, ArcanaCardMode mode,
            bool selected = false, UnitState live = null)
        {
            var bounds = Panel(parent, x0, y0, x1, y1, Color.clear);
            bounds.gameObject.name = "Card " + card.id;
            var texture = OriginalCardTexture(card);
            var surface = Panel(bounds, 0, 0, 1, 1, Color.clear);
            surface.gameObject.name = "Original printed card surface " + card.id;
            var shape = surface.GetComponent<UiRoundedImage>();
            shape.Radius = 0; shape.BorderWidth = 0; shape.TopTint = shape.BottomTint = Color.white;
            var fit = surface.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = texture != null ? (float)texture.width / texture.height : ArcanaCardAspect;
            var descriptor = surface.gameObject.AddComponent<FmArcanaCardSurface>();
            descriptor.DefinitionId = card.id; descriptor.Grade = card.rarity;
            descriptor.SourceAspect = fit.aspectRatio; descriptor.UsesFullPrint = texture != null;
            if (texture != null)
            {
                var print = new GameObject("Original complete print " + card.id, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                print.transform.SetParent(surface, false); Anchor((RectTransform)print.transform, 0, 0, 1, 1);
                print.texture = texture; print.uvRect = new Rect(0, 0, 1, 1); print.raycastTarget = false;
                print.color = Color.white;
            }
            else
            {
                Text(surface, card.name + "\n원본 이미지 없음", .05f, .1f, .95f, .9f, 24, ivory, TextAlignmentOptions.Center);
            }
            if (selected)
            {
                // Selection is a line outside the print; it does not recolour,
                // crop or cover the source's own engraved frame and grade.
                var halo = new GameObject("Selected original card outline", typeof(RectTransform), typeof(UiRoundedImage)).GetComponent<RectTransform>();
                halo.SetParent(surface, false); Anchor(halo, 0, 0, 1, 1);
                halo.offsetMin = new Vector2(-3, -3); halo.offsetMax = new Vector2(3, 3);
                var outline = halo.GetComponent<UiRoundedImage>(); outline.Fill = false; outline.Radius = 3;
                outline.BorderWidth = 2; outline.BorderColor = new Color(1, .91f, .58f); outline.raycastTarget = false;
            }
            return bounds;
        }

        private TMP_Text ArcanaText(Transform parent, string text, float x0, float y0, float x1, float y1,
            float size, Color tint, string role)
        {
            var label = MiniCardText(parent, text, x0, y0, x1, y1, size, tint, role);
            label.characterWidthAdjustment = 0;
            return label;
        }

        private void DrawArcanaCard(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1,
            bool selected, Action onClick, bool interactable)
        {
            var view = ArcanaCardFace(parent, card, x0, y0, x1, y1,
                battleScreen ? ArcanaCardMode.Hand : ArcanaCardMode.Album, selected);
            view.GetComponent<Image>().raycastTarget = true;
            var surface = view.GetComponentInChildren<FmArcanaCardSurface>().GetComponent<Image>();
            var button = view.gameObject.AddComponent<Button>(); button.targetGraphic = surface; button.interactable = interactable;
            // The original print always remains white and unmodified. Hover
            // feedback moves the whole fitted card instead of tinting its art.
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { FmBattleAudio.Play("card-select"); onClick(); });
            BindCardHelp(view.gameObject, card, () => ShowDetail(card, redraw));
            var hover = view.GetComponent<UiCardHover>(); hover.HoverScale = 1.045f; hover.HoverLift = 7;
        }
    }
}
