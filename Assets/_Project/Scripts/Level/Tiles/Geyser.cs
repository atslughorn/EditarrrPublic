﻿using System;
using UnityEngine;

namespace Editarrr.Level.Tiles
{
    [RequireComponent(typeof(Animator))]
    public class Geyser : MonoBehaviour
    {
        [field: SerializeField] private float Force { get; set; } = 100f;
        [SerializeField] Animator geyserAnimator, fountainEffectAnimator;
        [SerializeField] AudioSource _audioSource;
        [SerializeField] AudioClip _fountainEffectClip;

        const string ANIMATOR_TRIGGER_NAME = "Erupt";

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.TryGetComponent<IExternalForceReceiver>(out IExternalForceReceiver forceReceiver))
                return;

            forceReceiver.ReceiveImpulse(this.Force, this.transform.up);
            geyserAnimator.SetTrigger(ANIMATOR_TRIGGER_NAME);
            _audioSource.PlayOneShot(_fountainEffectClip);
        }

        public void StartFountainEffect() => fountainEffectAnimator.SetTrigger(ANIMATOR_TRIGGER_NAME);
    }
}
