using System;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelPilgrimageSaveTests
    {
        [Test]
        public void FieldPoseRoundTripsDepthWithoutRequestingAnotherMigration()
        {
            var game = GospelJourney.NewChronicleJourney();
            game.SetPresentationSpace(GospelJourney.PilgrimageSpace);
            game.SetPlayerPose(3.5f, .4f, -7.25f, 315);
            var loaded = GospelPilgrimageSave.Prepare(game.CaptureSave());
            Assert.That(loaded.RequiresFork, Is.False);
            Assert.That(loaded.CanRestorePose, Is.True);
            Assert.That(loaded.Journey.PlayerZ, Is.EqualTo(-7.25f));
            Assert.That(loaded.Journey.PlayerYaw, Is.EqualTo(315));
        }

        [Test]
        public void SideViewMigrationCopiesProgressAndDoesNotMutateOriginalPoseOrStory()
        {
            var game = GospelJourney.NewChronicleJourney();
            game.SetPresentationSpace(GospelJourney.LuminousSpace);
            game.SetPlayerPose(-21, 1.75f, 0, 180);
            while (game.Chronicle.DiscoveredCount == 0) game.Chronicle.AdvanceDialogue();
            var original = game.CaptureSave();
            var loaded = GospelPilgrimageSave.Prepare(original);
            Assert.That(loaded.RequiresFork, Is.True);
            Assert.That(loaded.CanRestorePose, Is.False);
            Assert.That(loaded.Journey.CodexCount, Is.EqualTo(1));
            Assert.That(loaded.Journey.Chronicle.CurrentQuest.Id, Is.EqualTo(game.Chronicle.CurrentQuest.Id));
            Assert.That(loaded.Journey.PlayerY, Is.Zero);
            Assert.That(original.presentationSpace, Is.EqualTo(GospelJourney.LuminousSpace));
            Assert.That(original.playerX, Is.EqualTo(-21));
            Assert.That(original.playerY, Is.EqualTo(1.75f));
            Assert.That(game.PresentationSpace, Is.EqualTo(GospelJourney.LuminousSpace));
            loaded.Journey.Chronicle.AdvanceDialogue();
            Assert.That(game.Chronicle.DialogueIndex, Is.EqualTo(original.chronicle.dialoguePage));
        }

        [Test]
        public void LegacyProgressImportsOnlyReadPassagesAndStillRequiresANewSlot()
        {
            var game = GospelJourney.New();
            while (game.CodexCount == 0) game.AdvanceDialogue();
            var original = game.CaptureSave();
            var loaded = GospelPilgrimageSave.Prepare(original);
            Assert.That(original.chronicle, Is.Null);
            Assert.That(loaded.Journey.Chronicle, Is.Not.Null);
            Assert.That(loaded.RequiresFork, Is.True);
            Assert.That(loaded.Journey.CodexCount, Is.EqualTo(game.CodexCount));
            Assert.That(loaded.Journey.CodexCount, Is.LessThan(19));
        }

        [Test]
        public void UnknownPresentationVersionIsRejectedWithoutChangingTheInput()
        {
            var original = GospelJourney.NewChronicleJourney().CaptureSave();
            original.presentationSpace = "pilgrimage-field-v999";
            Assert.Throws<ArgumentException>(() => GospelPilgrimageSave.Prepare(original));
            Assert.That(original.presentationSpace, Is.EqualTo("pilgrimage-field-v999"));
        }
    }
}
