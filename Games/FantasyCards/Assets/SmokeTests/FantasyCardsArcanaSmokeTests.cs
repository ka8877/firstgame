using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyCards.SmokeTests
{
    public sealed partial class FantasyCardsRevivalSmokeTests
    {
        [UnityTest]
        public IEnumerator RestorationCompleteOriginalCardsAndActualRoleChangesRetainTheirIdentity()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(605));
            Assert.That(DemoCatalog.StarterDecks.Length, Is.EqualTo(20));
            Assert.That(GameObject.Find("Arcana original character duo"), Is.Not.Null);
            yield return SmokeUi.Capture("FantasyCards", "restoration-critical-title", 1280, 720);
            FmOriginalMusic.SetMuted(false);
            SmokeUi.Click("게임 시작"); yield return null;
            var music = UnityEngine.Object.FindFirstObjectByType<FmLocalMusicHost>();
            Assert.That(music, Is.Not.Null);
            yield return SmokeUi.WaitUntil(() => music.ActiveScene == "lobby" && music.PlaybackPosition > 1.2f, 15, "Lobby audio must really advance.");
            yield return SmokeUi.Capture("FantasyCards", "restoration-critical-lobby", 1280, 720);
            SmokeUi.Click("카드 도감"); yield return null;
            yield return SmokeUi.WaitUntil(() => music.ActiveScene == "deck", 15, "Collection changes the actual music role.");
            yield return new WaitForSecondsRealtime(.6f);
            Canvas.ForceUpdateCanvases();
            AssertArcanaSurfaces();
            var startsBefore = music.PlaybackStarts.Length;
            float timeBefore = music.PlaybackPosition;
            SmokeUi.Click("원본 순서"); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(music.PlaybackStarts.Length, Is.EqualTo(startsBefore), "Same-role redraw cannot restart a song.");
            Assert.That(music.PlaybackPosition, Is.GreaterThan(timeBefore));
            yield return SmokeUi.Capture("FantasyCards", "restoration-critical-portrait-catalog", 1280, 720);
            SmokeUi.Click("대기실"); yield return null;
            yield return SmokeUi.WaitUntil(() => music.ActiveScene == "lobby" && music.PlaybackStarts.Length > startsBefore, 15, "Return to Lobby must start a new role instance.");
            var lobbyStart = music.PlaybackStarts.Last();
            Assert.That(lobbyStart.scene, Is.EqualTo("lobby"));
            Assert.That(lobbyStart.sourceTime, Is.EqualTo(0).Within(.001f));
            Assert.That(music.PlaybackPosition, Is.LessThan(1.2f), "Returning A-B-A starts A from zero instead of resuming.");
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../../../Artifacts/Validation/Restoration/native-music-transitions.json")), JsonUtility.ToJson(new ArcanaMusicLedger { starts = music.PlaybackStarts }, true));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }
        [Serializable] private sealed class ArcanaMusicLedger { public FmLocalMusicHost.PlaybackStart[] starts; }
        [Serializable] private sealed class ArcanaLabelDump { public ArcanaLabel[] labels; }
        [Serializable] private sealed class ArcanaLabel { public string name,text; public float fontSize,min,max,width,height,scale; public bool truncated; public int characters,visible; }
        static void AssertArcanaSurfaces()
        {
            var surfaces = UnityEngine.Object.FindObjectsByType<FmArcanaCardSurface>(FindObjectsSortMode.None).Where(s => s.gameObject.activeInHierarchy).ToArray();
            Assert.That(surfaces, Is.Not.Empty);
            var dump = new ArcanaLabelDump { labels = surfaces.SelectMany(surface => surface.GetComponentsInChildren<TMPro.TMP_Text>())
                .Select(label => new ArcanaLabel { name=label.name, text=label.text, fontSize=label.fontSize,
                    min=label.fontSizeMin, max=label.fontSizeMax, width=label.rectTransform.rect.width,
                    height=label.rectTransform.rect.height, scale=label.GetComponentInParent<Canvas>().rootCanvas.scaleFactor,
                    truncated=label.isTextTruncated, characters=label.textInfo.characterCount,
                    visible=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(glyph=>glyph.isVisible) }).ToArray() };
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../../../Artifacts/Validation/Restoration/native-arcana-labels.json")), JsonUtility.ToJson(dump,true));
            foreach (var surface in surfaces)
            {
                var definition=CardCatalog.Get(surface.DefinitionId);
                var texture=Resources.Load<Texture2D>(definition.artResource);
                Assert.That(texture,Is.Not.Null,surface.DefinitionId);
                Assert.That(surface.UsesFullPrint,Is.True,surface.DefinitionId);
                Assert.That(surface.SourceAspect,Is.EqualTo((float)texture.width/texture.height).Within(.0001f));
                Assert.That(surface.RenderedAspect,Is.EqualTo(surface.SourceAspect).Within(.015f),surface.DefinitionId);
                Assert.That(surface.Grade,Is.EqualTo(definition.rarity));
                var art=surface.GetComponentsInChildren<UnityEngine.UI.RawImage>();
                Assert.That(art.Length,Is.EqualTo(1),"One complete original print replaces the old recomposed crop and frames.");
                Assert.That(art[0].texture,Is.SameAs(texture));
                Assert.That(art[0].uvRect,Is.EqualTo(new Rect(0,0,1,1)),"Every original printed pixel stays within the full normalized source rectangle.");
                Assert.That(art[0].color,Is.EqualTo(Color.white),"The source grade/frame colors must not be retinted.");
                Assert.That(surface.GetComponentsInChildren<UnityEngine.UI.Mask>(),Is.Empty);
            }
        }
    }
}
