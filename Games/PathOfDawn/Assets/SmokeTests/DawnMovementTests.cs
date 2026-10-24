using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PathOfDawn.SmokeTests
{
    public sealed class DawnMovementTests
    {
        [Test]
        public void ReversingRespondsWithinOneTenthSecondWithoutWaitingForFacing()
        {
            var motor = new DawnMovement();
            motor.Step(Vector2.right, .3f);
            motor.Step(Vector2.left, .1f);
            Assert.That(motor.Velocity.x, Is.LessThan(-1.5f));
        }

        [Test]
        public void ReleasingStopsWithinSevenHundredthsOfAWorldUnit()
        {
            var motor = new DawnMovement();
            motor.Step(Vector2.right, .3f);
            Vector3 stopping = motor.Step(Vector2.zero, .1f);
            Assert.That(stopping.x, Is.InRange(0, .07f));
            Assert.That(motor.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(motor.Step(Vector2.zero, 1), Is.EqualTo(Vector3.zero));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void EquivalentInputHasSameTravelAcrossFrameRates(int fps)
        {
            Vector3 Drive(int rate)
            {
                var motor = new DawnMovement();
                Vector3 result = Vector3.zero;
                foreach (var input in new[] { Vector2.right, Vector2.left, Vector2.zero })
                    for (int i = 0; i < rate; ++i) result += motor.Step(input, 1f / rate);
                return result;
            }
            Assert.That(Vector3.Distance(Drive(fps), Drive(1000)), Is.LessThan(.001f));
        }

        [Test]
        public void DiagonalAndAnalogInputKeepProportionalScreenSpeed()
        {
            var motor = new DawnMovement();
            motor.Step(new Vector2(1, 1), 1);
            Assert.That(motor.Velocity.magnitude, Is.EqualTo(DawnMovement.Speed).Within(.0001f));
            motor.Reset(); motor.Step(Vector2.up * .5f, 1);
            Assert.That(motor.Velocity.magnitude, Is.EqualTo(DawnMovement.Speed * .5f).Within(.0001f));
            Assert.That(DawnMovement.StickValue(new Vector2(.08f, .04f)), Is.EqualTo(Vector2.zero));
            Assert.That(DawnMovement.StickValue(Vector2.up * .52f).y, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(DawnMovement.StickValue(Vector2.up * .95f), Is.EqualTo(Vector2.up));
        }

        [Test]
        public void TouchOwnerAndCancellationCannotLeaveAStuckJoystick()
        {
            var events = new GameObject("Motion test EventSystem", typeof(EventSystem));
            var go = new GameObject("Motion test pad", typeof(RectTransform), typeof(DawnPointerPad));
            try
            {
                var pad = go.GetComponent<DawnPointerPad>(); pad.joystick = true;
                var first = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 5, position = Vector2.right * 76 };
                var other = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 9, position = Vector2.left * 76 };
                pad.OnPointerDown(first); Assert.That(pad.Value.x, Is.GreaterThan(.99f));
                pad.OnDrag(other); pad.OnPointerUp(other); Assert.That(pad.Value.x, Is.GreaterThan(.99f));
                pad.OnCancel(new BaseEventData(events.GetComponent<EventSystem>())); Assert.That(pad.Value, Is.EqualTo(Vector2.zero));
                pad.OnPointerDown(other); Assert.That(pad.Value.x, Is.LessThan(-.99f));
                pad.OnPointerUp(other); Assert.That(pad.Value, Is.EqualTo(Vector2.zero));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(events); }
        }
    }
}
