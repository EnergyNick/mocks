using System;
using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary
            = new Dictionary<string, Thing>();
        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            Thing thing;
            if (dictionary.TryGetValue(thingId, out thing))
                return thing;
            
            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }
            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private IThingService thingService;
        private ThingCache thingCache;

        private const string thingId1 = "TheDress";
        private Thing thing1 = new Thing(thingId1);

        private const string thingId2 = "CoolBoots";
        private Thing thing2 = new Thing(thingId2);

        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            thingCache = new ThingCache(thingService);
        }

        [Test]
        public void ThingCache_ShouldReturnNull_WhenCallingEmpty()
        {
            thingCache.Get("vw").Should().BeNull();
        }

        [Test]
        public void ThingCache_ShouldReturnValueFromService_WhenCallingTag()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            thingCache.Get(thingId1).Should().Be(thing1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappened();
        }

        [Test]
        public void ThingCache_ShouldReturnValueFromServiceAndCache_WhenCallingTagTwice()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            thingCache.Get(thingId1).Should().Be(thing1);
            thingCache.Get(thingId1).Should().Be(thing1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ThingCache_ShouldReturnTags_WhenCallingTagTwice()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            A.CallTo(() => thingService.TryRead(thingId2, out thing2)).Returns(true);
            thingCache.Get(thingId1).Should().Be(thing1);
            thingCache.Get(thingId2).Should().Be(thing2);
            
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out thing2))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
        public void ThingCache_ShouldReturnNull_WhenCallWithNull()
        {
            A.CallTo(() => thingService.TryRead(null, out thing1)).Returns(false);
            Action test = () => thingCache.Get(null);
            test.ShouldThrow<ArgumentNullException>();
        }

        [Test]
        public void DoSomething_WhenSomething()
        {
            thingCache.Get(thingId1).Should().BeNull();
            thingCache.Get(thingId1).Should().BeNull();
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappened(Repeated.Exactly.Twice);
        }
        
        //TODO: написать простейший тест, а затем все остальные
        //Live Template tt работает!
    }
}