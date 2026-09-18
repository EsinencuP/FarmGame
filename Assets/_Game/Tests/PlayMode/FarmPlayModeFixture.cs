using System;
using System.Collections;
using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    public abstract class FarmPlayModeFixture
    {
        private Scene _previousScene;
        private Scene _testScene;
        private readonly List<GameObject> _otherFarms = new List<GameObject>();
        protected GameBootstrap Farm { get; private set; }

        [UnitySetUp]
        public IEnumerator CreateIsolatedFarm()
        {
            _previousScene = SceneManager.GetActiveScene();
            foreach (var other in UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
            {
                _otherFarms.Add(other.gameObject);
                other.gameObject.SetActive(false);
            }
            _testScene = SceneManager.CreateScene("Farm test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(_testScene);
            // A non-Prototype scene does not start persistence or read the player's save.
            Farm = new GameObject("Test Farm").AddComponent<GameBootstrap>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DestroyIsolatedFarm()
        {
            if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
            if (_testScene.IsValid() && _testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_testScene);
            foreach (var other in _otherFarms) if (other != null) other.SetActive(true);
            _otherFarms.Clear();
            Farm = null;
        }
    }
}
