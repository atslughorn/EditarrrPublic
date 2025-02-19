using System;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

namespace Gameplay
{
    public class Chest : MonoBehaviour
    {
        public static event Action OnChestOpened;

        [SerializeField] AudioClip winSound;

        private Animator _animator;

        private int _remainingKeys;
        private bool _isWon;
        private bool _isOpen;
        private static readonly int Open = Animator.StringToHash("Open");

        private void Start()
        {
            _animator = GetComponent<Animator>();
            Invoke(nameof(InitializeState), 0.5f);
        }

        private void InitializeState()
        {
            ManageKeys();
        }

        private void ManageKeys(int countChange = 0)
        {
            _remainingKeys += countChange;

            if (_remainingKeys == 0)
                SetOpen();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isWon || !_isOpen)
            {
                return;
            }

            // The Chest should be on the Chest layer and the Player should be on the player layer.
            // The Chest should ONLY be allowed to collide with the Player and so no further checks
            // are required.
            // @todo Check this by (for example) letting an Enemy run into the Chest.
            _isWon = true;
            Editarrr.Audio.AudioManager.Instance.PlayAudioClip(winSound);
            OnChestOpened?.Invoke();
        }

        public void SetOpen()
        {
            if (_isOpen)
            {
                return;
            }

            _animator.SetTrigger(Open);
            _isOpen = true;
        }

        private void OnEnable()
        {
            Key.OnKeyCountChanged += ManageKeys;
        }

        private void OnDisable()
        {
            Key.OnKeyCountChanged -= ManageKeys;
        }
    }
}
