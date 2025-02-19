using System;
using Browser;
using Editarrr.Audio;
using Editarrr.Input;
using Editarrr.Level;
using Level.Storage;
using Singletons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.GUI
{
    public class WinMenu : MonoBehaviour
    {
        public static event Action<string, RemoteScoreStorage_AllScoresLoadedCallback> OnLeaderboardRequested;

        public delegate void WinMenu_OnScoreSubmit();
        public static event Action<string, float, WinMenu_OnScoreSubmit> OnScoreSubmit;
        public static event Action<string, int> OnRatingSubmit;

        public TMP_Text LevelCode;
        public TMP_Text TimerText;
        public Button HomeButton, BackEditorButton, ReplayButton;
        public Button ShowLeaderboardButton;
        public bool IsReplay = false;

        [SerializeField] Animator _animator;
        [SerializeField] GameObject _leaderBoard;
        [SerializeField] RatingMenu _ratingMenu;
        [field: SerializeField, Tooltip("Pause input")] private InputValue PauseInput { get; set; }

        const string VICTORY_TRIGGER_NAME = "Victory";

        private string _code;
        private string _user;
        private float _time;
        private string _timeText;
        private LevelState _levelData;
        private void Update()
        {
            if (PauseInput.WasPressed)
                OnClickHome();
        }

        private void UpdateContent()
        {
            DisableAllButtons();
            LevelCode.text = _code.ToUpper();

            if (_levelData == null)
            {
                return;
            }

            if (_levelData.CreatorName.Length > 0)
            {
                _user = _levelData.CreatorName;
                LevelCode.text += " by " + _user;
            }

            // Home is always a valid option.
            HomeButton.interactable = true;

            if (IsReplay)
            {
                ReplayButton.interactable = true;
                ShowLeaderboardButton.interactable = false;
            }
            else if (_levelData.Published)
            {
                ReplayButton.interactable = true;
                ShowLeaderboardButton.interactable = true;
            }
            else
            {
                ReplayButton.interactable = true;
                BackEditorButton.interactable = true;
                ShowLeaderboardButton.interactable = false;
            }
        }

        private void DisableAllButtons()
        {
            ReplayButton.interactable = false;
            ShowLeaderboardButton.interactable = false;
            HomeButton.interactable = false;
            BackEditorButton.interactable = false;
        }

        public void Show()
        {
            UpdateContent();
            _animator.SetTrigger(VICTORY_TRIGGER_NAME);

            string currentUser = PreferencesManager.Instance.GetUserName();
            // @todo only show this if its not the players own level.
            if (_user != currentUser)
            {
                AchievementManager.Instance.UnlockAchievement(GameAchievement.LevelCompleted);
            }
        }

        public void SetCode(string code)
        {
            _code = code;

            UpdateContent();
        }

        public void SetLevelData(LevelState levelData)
        {
            _levelData = levelData;

            UpdateContent();
        }

        private void SetTimeText(float time, string timeText)
        {
            _time = time;
            _timeText = timeText;
            if (TimerText != null)
            {
                TimerText.text = timeText;
            }

            SubmitScore();

            string currentUser = PreferencesManager.Instance.GetUserName();
            TwitchManager.Instance.SendNotification($"{currentUser} just finished level {_code} in {_timeText}.");
        }

        public void SubmitScore()
        {
            Debug.Log("Submitting score");
            OnScoreSubmit?.Invoke(_code, _time, OnSubmitScoreOpenDashboard);

            void OnSubmitScoreOpenDashboard()
            {
                // We dont need this anymore.
            }
        }

        public void OpenScoreboard()
        {
            _leaderBoard.SetActive(true);

            var leaderboard = _leaderBoard.GetComponent<LeaderboardForm>();
            leaderboard.SetCode(this._code);

            OnLeaderboardRequested?.Invoke(this._code, LeaderboardScoresLoaded);

            void LeaderboardScoresLoaded(ScoreStub[] scoreStubs)
            {
                leaderboard.SetScores(scoreStubs);
            }

            AudioManager.Instance.PlayAudioClip(AudioManager.BUTTONCLICK_CLIP_NAME);
        }

        public void OpenRatingMenu()
        {
            _ratingMenu.OpenMenu(_code);
            AudioManager.Instance.PlayAudioClip(AudioManager.AFFIRMATIVE_CLIP_NAME);
        }

        public void CloseRatingMenu()
        {
            _ratingMenu.UpdateRating(0);
            _ratingMenu.gameObject.SetActive(false);
            AudioManager.Instance.PlayAudioClip(AudioManager.NEGATIVE_CLIP_NAME);
        }

        #region ButtonTriggers

        public void OnClickReplay()
        {
            SceneTransitionManager.Instance.GoToScene(SceneTransitionManager.TestLevelSceneName);
        }

        public void OnClickShowLeaderboard()
        {
            if (PreferencesManager.Instance.HasLevelRating(_code))
                OpenScoreboard();
            else
                OpenRatingMenu();
        }

        public void OnClickHome()
        {
            SceneTransitionManager.Instance.GoToScene(SceneTransitionManager.LevelSelectionSceneName);
        }

        public void OnClickBackEditor()
        {
            SceneTransitionManager.Instance.GoToScene(SceneTransitionManager.CreateLevelSceneName);
        }

        public void SubmitRating(int rating)
        {
            OnRatingSubmit?.Invoke(_code, rating);
            OpenScoreboard();
        }
        #endregion

        protected void OnEnable()
        {
            Timer.OnTimeStop += SetTimeText;
            ShowLeaderboardButton.onClick.AddListener(OnClickShowLeaderboard);
        }

        protected void OnDisable()
        {
            Timer.OnTimeStop -= SetTimeText;
            ShowLeaderboardButton.onClick.RemoveAllListeners();
        }

        public void DeactivateWinMenuAnimator() => _animator.enabled = false;
    }
}
