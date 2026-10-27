using System;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelBelongingsTests
    {
        [Test]
        public void OldSchemaOneSaveOpensWithoutEquippedToolAndNewEquipRoundTrips()
        {
            var original=GospelJourney.New().CaptureSave();
            Assert.That(original.equippedUtility,Is.Null);
            var journey=GospelJourney.FromSave(original);
            Assert.That(journey.LampEquipped,Is.False);
            string[] readings=journey.CollectedReadings.ToArray();
            journey.SetUtility(GospelBelongings.LampId);
            var copy=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(FieldJson.Encode(journey.CaptureSave())));
            Assert.That(copy.LampEquipped,Is.True);
            Assert.That(copy.CollectedReadings,Is.EqualTo(readings));
            Assert.That(copy.DialogueIndex,Is.EqualTo(journey.DialogueIndex));
            copy.SetUtility("");
            Assert.That(GospelJourney.FromSave(copy.CaptureSave()).LampEquipped,Is.False);
        }

        [Test]
        public void UnsupportedToolCannotSilentlyEnterSaveOrOverwriteExistingProgress()
        {
            var journey=GospelJourney.New();
            journey.SetUtility(GospelBelongings.LampId);
            Assert.Throws<ArgumentException>(()=>journey.SetUtility("unknown-weapon"));
            Assert.That(journey.LampEquipped,Is.True);
            var bad=journey.CaptureSave();bad.equippedUtility="unknown-weapon";
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(bad));
            Assert.That(journey.LampEquipped,Is.True);
        }
    }
}
