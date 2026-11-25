using HowToHunt.Core;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    /// <summary>
    /// The owner's rule for what is drawn (2026-11-25), on the chain lab 0, Plains 1, east route 2, Pine Forest 3,
    /// south route 4, Red Desert 5: the current map in full, its neighbours bare, the routes to them in full.
    /// </summary>
    public sealed class AreaStreamingTests
    {
        private const int Lab = 0, Plains = 1, East = 2, Forest = 3, South = 4, Desert = 5;

        private static StreamedArea.Mode Map(int order, int here, int prev, int next) => StreamedArea.ModeFor(order, false, here, false, prev, next);
        private static StreamedArea.Mode Route(int order, int here, int prev, int next) => StreamedArea.ModeFor(order, true, here, false, prev, next);

        [Test]
        public void InTheDesert_TheForestIsBareAndThePlainsAreGone()
        {
            const int prev = Forest, next = int.MaxValue;
            Assert.AreEqual(StreamedArea.Mode.Full, Map(Desert, Desert, prev, next));
            Assert.AreEqual(StreamedArea.Mode.Bare, Map(Forest, Desert, prev, next));
            Assert.AreEqual(StreamedArea.Mode.Full, Route(South, Desert, prev, next));
            Assert.AreEqual(StreamedArea.Mode.Hidden, Route(East, Desert, prev, next));
            Assert.AreEqual(StreamedArea.Mode.Hidden, Map(Plains, Desert, prev, next));
            Assert.AreEqual(StreamedArea.Mode.Hidden, Map(Lab, Desert, prev, next));
        }

        [Test]
        public void InTheForest_BothRoutesAndBothNeighbours()
        {
            Assert.AreEqual(StreamedArea.Mode.Bare, Map(Plains, Forest, Plains, Desert));
            Assert.AreEqual(StreamedArea.Mode.Bare, Map(Desert, Forest, Plains, Desert));
            Assert.AreEqual(StreamedArea.Mode.Full, Route(East, Forest, Plains, Desert));
            Assert.AreEqual(StreamedArea.Mode.Full, Route(South, Forest, Plains, Desert));
            Assert.AreEqual(StreamedArea.Mode.Hidden, Map(Lab, Forest, Plains, Desert));
        }

        [Test]
        public void InThePlains_TheLabIsBareAndTheDesertIsGone()
        {
            Assert.AreEqual(StreamedArea.Mode.Bare, Map(Lab, Plains, Lab, Forest));
            Assert.AreEqual(StreamedArea.Mode.Bare, Map(Forest, Plains, Lab, Forest));
            Assert.AreEqual(StreamedArea.Mode.Full, Route(East, Plains, Lab, Forest));
            Assert.AreEqual(StreamedArea.Mode.Hidden, Route(South, Plains, Lab, Forest));
            Assert.AreEqual(StreamedArea.Mode.Hidden, Map(Desert, Plains, Lab, Forest));
        }

        [Test]
        public void OnASurfRoute_TheWholeCourseAndBothEndsAreDrawn()
        {
            // On the east route: Plains and Pine Forest in full (the pit's trees belong to the forest), nothing else.
            Assert.AreEqual(StreamedArea.Mode.Full, StreamedArea.ModeFor(East, true, East, true, Plains, Forest));
            Assert.AreEqual(StreamedArea.Mode.Full, StreamedArea.ModeFor(Plains, false, East, true, Plains, Forest));
            Assert.AreEqual(StreamedArea.Mode.Full, StreamedArea.ModeFor(Forest, false, East, true, Plains, Forest));
            Assert.AreEqual(StreamedArea.Mode.Hidden, StreamedArea.ModeFor(South, true, East, true, Plains, Forest));
            Assert.AreEqual(StreamedArea.Mode.Hidden, StreamedArea.ModeFor(Desert, false, East, true, Plains, Forest));
            Assert.AreEqual(StreamedArea.Mode.Hidden, StreamedArea.ModeFor(Lab, false, East, true, Plains, Forest));
        }
    }
}
