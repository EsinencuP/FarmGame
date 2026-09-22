using System;
using System.Collections;
using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MyLittleFarm.Tests.PlayMode
{
    /// <summary>Создаёт отдельную сцену и чистый экземпляр фермы для каждого PlayMode-теста.</summary>
    public abstract class FarmPlayModeFixture
    {
        // Предыдущая и тестовая сцены нужны для безопасного возврата после проверки.
        private Scene _previousScene;
        private Scene _testScene;
        // Уже существующие фермы временно выключаются, чтобы не влиять на поиск компонентов и физику.
        private readonly List<GameObject> _otherFarms = new List<GameObject>();
        // Защищённое свойство даёт наследникам доступ к единственному тестовому корню.
        protected GameBootstrap Farm { get; private set; }

        [UnitySetUp]
        public IEnumerator CreateIsolatedFarm()
        {
            // Имя сцены намеренно отличается от Prototype, поэтому пользовательское сохранение не загружается.
            _previousScene = SceneManager.GetActiveScene();
            foreach (var other in UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
            {
                _otherFarms.Add(other.gameObject);
                other.gameObject.SetActive(false);
            }
            _testScene = SceneManager.CreateScene("Farm test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(_testScene);
            // Пустая тестовая сцена явно вызывает генератор и не читает пользовательское сохранение.
            Farm = new GameObject("Test Farm").AddComponent<GameBootstrap>();
            Farm.BuildPrototype();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DestroyIsolatedFarm()
        {
            // Восстанавливает активную сцену и состояние объектов даже после неуспешного теста.
            if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
            if (_testScene.IsValid() && _testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_testScene);
            foreach (var other in _otherFarms) if (other != null) other.SetActive(true);
            _otherFarms.Clear();
            Farm = null;
        }
    }
}
