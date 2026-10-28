using System;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelLuminousSaveTests
    {
        [Test]
        public void OlderSchemaOneWithoutPresentationSpaceRetainsScriptureAndOriginalPose()
        {
            var old = GospelJourney.New();
            old.AdvanceReading();
            old.SetPlayerPose(8, .2f, -7, 90);
            var json = FieldJson.Encode(old.CaptureSave()).Replace(",\"presentationSpace\":null", "");
            Assert.That(json, Does.Not.Contain("presentationSpace"));
            var restored = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(json));
            Assert.That(restored.PresentationSpace, Is.Empty);
            Assert.That(restored.PlayerZ, Is.EqualTo(-7));
            Assert.That(restored.CollectedReadings, Is.EqualTo(old.CollectedReadings));
            Assert.That(restored.ProloguePage, Is.EqualTo(1));
        }

        [Test]
        public void LuminousSpaceRoundTripsWithoutChangingReadingOrEquipment()
        {
            var journey = GospelJourney.New();
            journey.AdvanceReading();
            journey.SetUtility(GospelBelongings.LampId);
            journey.SetPresentationSpace(GospelJourney.LuminousSpace);
            journey.SetPlayerPose(-18, .15f, 0, 180);
            var loaded = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(FieldJson.Encode(journey.CaptureSave())));
            Assert.That(loaded.PresentationSpace, Is.EqualTo(GospelJourney.LuminousSpace));
            Assert.That(loaded.PlayerX, Is.EqualTo(-18));
            Assert.That(loaded.LampEquipped, Is.True);
            Assert.That(loaded.CurrentReading.Id, Is.EqualTo(journey.CurrentReading.Id));
        }

        [Test]
        public void UnknownCoordinateSpaceCannotReplaceCurrentJourney()
        {
            var journey = GospelJourney.New();
            var before = FieldJson.Encode(journey.CaptureSave());
            var invalid = journey.CaptureSave();
            invalid.presentationSpace = "unknown-space";
            Assert.Throws<ArgumentException>(() => journey.RestoreSave(invalid));
            Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
        }
    }
}
