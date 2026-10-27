using System;
using TMPro;
using UnityEngine;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private static string ButtonCue(string label)
        {
            if (label == "돌아가기" || label == "닫기" || label == "취소") return "cancel";
            if (label == "이대로 시작" || label == "준비 완료") return "confirm";
            return "click";
        }
        private void ShowSoundSettings(Action back)
        {
            redraw = () => ShowSoundSettings(back); Clear(); Backdrop(true);
            var panel = Panel(root, .10f, .13f, .90f, .91f, ink); Frame(panel, gold, 1.5f);
            Text(panel, "소리 설정", .055f, .83f, .945f, .97f, 40, ivory, TextAlignmentOptions.Center);
            Text(panel, "배경 음악과 효과음은 따로 조절할 수 있습니다.", .055f, .735f, .945f, .83f, 24, gold, TextAlignmentOptions.Center);
            Text(panel, "효과음  " + Mathf.RoundToInt(FmBattleAudio.Volume * 100) + "%", .055f, .59f, .50f, .72f, 28, ivory);
            Button(panel, FmBattleAudio.Muted ? "효과음 켜기" : "효과음 끄기", .52f, .59f, .945f, .72f,
                () => { bool muted = !FmBattleAudio.Muted; FmBattleAudio.SetMuted(muted);
                    if (!muted) FmBattleAudio.Play("confirm"); ShowSoundSettings(back); }, slate, 25);
            Button(panel, "효과음 −", .055f, .42f, .33f, .56f,
                () => { FmBattleAudio.SetVolume(FmBattleAudio.Volume - .1f); FmBattleAudio.Play("card-select"); ShowSoundSettings(back); }, slate, 24);
            Button(panel, "효과음 +", .36f, .42f, .635f, .56f,
                () => { FmBattleAudio.SetVolume(FmBattleAudio.Volume + .1f); FmBattleAudio.Play("card-select"); ShowSoundSettings(back); }, slate, 24);
            Button(panel, "전투 효과 듣기", .665f, .42f, .945f, .56f,
                () => FmBattleAudio.Play("spell"), teal, 24, !FmBattleAudio.Muted && FmBattleAudio.Volume > 0);
            Text(panel, "카드·코인·공격·방어·마법의 소리를 구분합니다.", .055f, .285f, .945f, .39f, 22,
                new Color(.75f, .83f, .91f), TextAlignmentOptions.Center);
            Button(panel, "배경 음악 설정", .055f, .09f, .49f, .245f,
                () => ShowMusicSettings(() => ShowSoundSettings(back)), slate, 25);
            Button(panel, "돌아가기", .52f, .09f, .945f, .245f, back, gold, 25);
        }
    }
}
