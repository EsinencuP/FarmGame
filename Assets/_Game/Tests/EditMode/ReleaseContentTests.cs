using System;
using MyLittleFarm.Audio;
using MyLittleFarm.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Tests.EditMode
{
    public sealed class ReleaseContentTests
    {
        [Test]
        public void ComicHasSevenPagesAtTheirOriginalProportions()
        {
            var story = Resources.Load<ComicProloguePages>("FarmUI/ComicPrologue");
            Assert.That(story, Is.Not.Null);
            var pages = new SerializedObject(story).FindProperty("pages");
            Assert.That(pages.arraySize, Is.EqualTo(7));
            for (var index = 0; index < pages.arraySize; index++)
            {
                var page = pages.GetArrayElementAtIndex(index).objectReferenceValue as Texture2D;
                Assert.That(page, Is.Not.Null, $"Missing story page {index + 1}");
                Assert.That((float)page.width / page.height, Is.EqualTo(16f / 9f).Within(.015f));
            }
            Assert.That(pages.GetArrayElementAtIndex(6).objectReferenceValue.name, Is.EqualTo("Новая жизнь на ферме"));
        }

        [Test]
        public void EveryAudioCueHasAPlayableClipAndAMixerBus()
        {
            var catalog = Resources.Load<FarmAudioCatalog>("Audio/FarmAudioCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.mixer, Is.Not.Null);
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                var cue = catalog.Find(id);
                Assert.That(cue, Is.Not.Null, id.ToString());
                Assert.That(catalog.Group(cue.bus), Is.Not.Null, id.ToString());
                Assert.That(cue.clips, Is.Not.Empty, id.ToString());
                foreach (var clip in cue.clips)
                {
                    Assert.That(clip, Is.Not.Null, id.ToString());
                    Assert.That(clip.samples, Is.Positive, id.ToString());
                }
            }
        }
    }
}
