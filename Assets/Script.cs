using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AstraLink.UI
{
    public class IntroController : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage rawImage;
        private string nextSceneName = "Menu";
        private float fixedDuration = 23f;
        private float skipDelay = 0.5f;

        private bool _isTransitioning;
        private bool _canSkip;

        private void Start()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }

            if (rawImage != null)
            {
                RectTransform rt = rawImage.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = true;
                videoPlayer.isLooping = false;
                videoPlayer.Play();
            }

            StartCoroutine(TimerRoutine());
            StartCoroutine(EnableSkipAfterDelay());
        }

        private IEnumerator TimerRoutine()
        {
            yield return new WaitForSecondsRealtime(fixedDuration);
            LoadNextScene();
        }

        private IEnumerator EnableSkipAfterDelay()
        {
            yield return new WaitForSecondsRealtime(skipDelay);
            _canSkip = true;
        }

        private void Update()
        {
            if (!_canSkip || _isTransitioning) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                LoadNextScene();
            }
        }

        private void LoadNextScene()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            SceneManager.LoadScene(nextSceneName);
        }
    }
}