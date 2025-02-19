using System;
using Editarrr.Audio;
using UnityEngine;
using Yanniboi.Twitch;

namespace Twitch
{
    public class BarkCommand : CommandBase
    {

        public override string CommandName => "!bark";

        protected override void DoExecute(string user, string message)
        {
            if (message.Contains("booty"))
            {
                AudioManager.Instance.PlayAudioClip("Booty01");
            }

            if (message.Contains("arrr"))
            {
                AudioManager.Instance.PlayAudioClip("Arrr01");
            }

            if (message.Contains("yohoho"))
            {
                AudioManager.Instance.PlayAudioClip("YoHoHo01");
            }

            if (message.Contains("yarr"))
            {
                AudioManager.Instance.PlayAudioClip("Yarr01");
            }

            if (message.Contains("stupid"))
            {
                AudioManager.Instance.PlayAudioClip("stupid");
            }
        }

    }
}
