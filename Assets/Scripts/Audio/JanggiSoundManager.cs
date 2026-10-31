using System.Collections;
using UnityEngine;

namespace Janggi
{
    /// <summary>
    /// 절묘한 모바일 장기 사운드 및 TTS/목소리 대사 효과음 합성 시스템 (Procedural Audio Synth)
    /// 외부 오디오 파일 없이 절묘한 효과음 및 한국어 억양의 TTS 음성 대사를 생성 및 재생합니다.
    /// </summary>
    public class JanggiSoundManager : MonoBehaviour
    {
        public static JanggiSoundManager Instance { get; private set; }

        private AudioSource sfxSource;
        private AudioSource voiceSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitAudioSources();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitAudioSources()
        {
            // 오디오 시스템 글로벌 48kHz 고해상도 리샘플링 셋업
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.sampleRate = 48000;
            config.dspBufferSize = 512; // 반응속도 및 고음질 버퍼
            AudioSettings.Reset(config);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f; // 2D Sound (모든 곳에서 잘 들리도록)
            sfxSource.volume = 1.0f;
            sfxSource.priority = 0; // 최상위 명확도 우선순위

            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f; // 2D Sound
            voiceSource.volume = 1.0f;
            voiceSource.priority = 0; // 최상위 명확도 우선순위

            // AudioListener 자동 점검 및 부착
            if (FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
        }

        #region 1. 착수 및 기물 이동 효과음 (Move & Capture SFX)

        /// <summary>
        /// 장기말 이동 및 탁! 내려놓는 목재 착수음
        /// </summary>
        public void PlayMoveSound()
        {
            int sampleRate = 44100;
            float duration = 0.12f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            // 묵직한 나무 탁! 소리 (낮은 주파수 임팩트 + 정갈한 감쇄)
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 35f); // 빠른 어택과 디케이
                float freq1 = Mathf.Sin(2f * Mathf.PI * 160f * t);
                float freq2 = Mathf.Sin(2f * Mathf.PI * 340f * t);
                float noise = (Random.value * 2f - 1f) * 0.15f * Mathf.Exp(-t * 80f);

                data[i] = (freq1 * 0.6f + freq2 * 0.3f + noise) * env;
            }

            AudioClip clip = AudioClip.Create("MoveSFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sfxSource.PlayOneShot(clip, 0.9f);
        }

        /// <summary>
        /// 상대 말을 잡아챌 때의 강렬한 팍! 착수음
        /// </summary>
        public void PlayCaptureSound()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            // 강렬한 타격감 (스맥/임팩트 타격음 + 잔향)
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 28f);
                float body = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.7f;
                float transient = Mathf.Sin(2f * Mathf.PI * 480f * t) * 0.4f;
                float snap = (Random.value * 2f - 1f) * 0.4f * Mathf.Exp(-t * 120f);

                data[i] = (body + transient + snap) * env;
            }

            AudioClip clip = AudioClip.Create("CaptureSFX", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sfxSource.PlayOneShot(clip, 1.0f);
        }

        #endregion

        #region 2. 장이야! 멍이야! 및 대국 종료/시작 이벤트 음성/효과음

        /// <summary>
        /// 대국 시작 시 인사 및 시작 음성
        /// </summary>
        public void PlayGameStartVoice()
        {
            SpeakFreeTTS("자 이제 한수 가르침을 부탁드립니다.");
        }

        /// <summary>
        /// "장이야!" 긴장감 넘치는 무료 구글 성우 음성
        /// </summary>
        public void PlayJangiyoVoice()
        {
            SpeakFreeTTS("장이야!");
            StartCoroutine(GenerateAndPlayVoice("JANGIYO", 0.65f, 180f, 1.2f));
        }

        /// <summary>
        /// "멍이야!" 쾌통한 응수 음성
        /// </summary>
        public void PlayMungiyoVoice()
        {
            SpeakFreeTTS("멍이야!");
            StartCoroutine(GenerateAndPlayVoice("MUNGIYO", 0.55f, 220f, 1.0f));
        }

        /// <summary>
        /// 플레이어 승리 시 빵빠레 및 축하 음성 효과
        /// </summary>
        public void PlayPlayerWinVoice()
        {
            PlayFanfareSFX();
            SpeakFreeTTS("축하합니다! 승리하셨습니다!");
        }

        /// <summary>
        /// 플레이어 패배 시 코믹하게 약올리는 깐족 음성 효과 (High-pitch Comic Voice)
        /// </summary>
        public void PlayPlayerLoseTauntVoice()
        {
            string[] comicTaunts = new string[] {
                "메롱! 장기 두는 사람 어디 갔나? 낄낄낄!",
                "에구구! 수가 너무 훤히 보인다 훤히 보여! 낄낄낄!",
                "훈수라도 좀 받아오지 그랬냐! 메롱!",
                "어라라! 또 졌네 또 졌어! 한 판 더 덤벼보시지!",
                "아이고! 왕을 그렇게 헌납하시면 어쩌나! 낄낄!"
            };
            string chosenTaunt = comicTaunts[Random.Range(0, comicTaunts.Length)];
            
            // 코믹한 헬륨가스/깐족이 음정(Pitch 1.28~1.38)으로 발성
            SpeakComicTTS(chosenTaunt, Random.Range(1.28f, 1.38f));

            // 코믹 낄낄 웃음소리 연타 사운드 함께 재생
            StartCoroutine(PlayTauntLaughSequence());
        }

        /// <summary>
        /// AI 분노(판 뒤엎기/폭발) 시 분노의 쾅! 소리
        /// </summary>
        public void PlayFlipRageSound()
        {
            int sampleRate = 44100;
            float duration = 0.8f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6f);
                float rumble = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.6f;
                float crash = (Random.value * 2f - 1f) * 0.7f * Mathf.Exp(-t * 12f);
                data[i] = (rumble + crash) * env;
            }

            AudioClip clip = AudioClip.Create("RageCrash", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            sfxSource.PlayOneShot(clip, 1.0f);
        }

        #endregion

        #region Helper Audio Generators

        private IEnumerator GenerateAndPlayVoice(string type, float duration, float baseFreq, float pitchMultiplier)
        {
            // 합성 오디오 사운드도 함께 웅장한 효과음으로 출력
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float normT = t / duration;

                // 음성 모음(Formant) & 억양 곡선 생성 (음절 단위 파형)
                float formantEnv = Mathf.Sin(normT * Mathf.PI);
                float pitchMod = baseFreq + Mathf.Sin(normT * Mathf.PI * 3.0f) * 60f;
                if (type == "JANGIYO")
                {
                    // 장이야!: "장!" (높은 어택) -> "이!" (떨림) -> "야!" (하강) 3음절 파동 모형
                    float syllablePattern = Mathf.Sin(normT * Mathf.PI * 3f);
                    pitchMod = baseFreq * (1.4f - normT * 0.6f) + syllablePattern * 35f;
                }
                else if (type == "WIN")
                {
                    pitchMod = baseFreq * (0.8f + normT * 0.8f);
                }

                // 음성 포만트 폼트 (Vocal Formants)
                float vocal1 = Mathf.Sin(2f * Mathf.PI * pitchMod * t);
                float vocal2 = Mathf.Sin(2f * Mathf.PI * (pitchMod * 2.0f) * t) * 0.5f;
                float vocal3 = Mathf.Sin(2f * Mathf.PI * (pitchMod * 3.0f) * t) * 0.3f;
                float grit = (Random.value * 2f - 1f) * 0.12f;

                data[i] = (vocal1 + vocal2 + vocal3 + grit) * formantEnv * 0.9f;
            }

            AudioClip clip = AudioClip.Create($"Voice_{type}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);

            voiceSource.pitch = pitchMultiplier;
            voiceSource.PlayOneShot(clip, 1.0f);
            yield return null;
        }

        /// <summary>
        /// 100% 무료 고품질 구글 한국어 성우 TTS (API 키 필요없음)
        /// </summary>
        public void SpeakFreeTTS(string textToSay)
        {
            if (string.IsNullOrEmpty(textToSay)) return;
            StartCoroutine(DownloadAndPlayFreeTTS(textToSay, 1.0f));
        }

        /// <summary>
        /// 코믹하고 깐족거리는 하이피치/헬륨 효과 한국어 음성
        /// </summary>
        public void SpeakComicTTS(string textToSay, float pitchMultiplier = 1.35f)
        {
            if (string.IsNullOrEmpty(textToSay)) return;
            StartCoroutine(DownloadAndPlayFreeTTS(textToSay, pitchMultiplier));
        }

        private IEnumerator DownloadAndPlayFreeTTS(string text, float pitch = 1.0f)
        {
            // TTS가 '~' 문자를 '물결표'라고 읽지 않도록 필터링 처리
            string cleanText = text.Replace("~", "").Replace("^", "").Replace("  ", " ");
            string encodedText = UnityEngine.Networking.UnityWebRequest.EscapeURL(cleanText);
            // 구글 gtx 신경망(Neural) 고해상도 24kHz 한국어 음성 스트림 URL
            string url = $"https://translate.google.com/translate_tts?ie=UTF-8&client=gtx&tl=ko-KR&idx=0&total=1&textlen={cleanText.Length}&q={encodedText}";

            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    if (clip != null && voiceSource != null)
                    {
                        voiceSource.pitch = pitch;
                        voiceSource.outputAudioMixerGroup = null;
                        voiceSource.PlayOneShot(clip, 1.0f);
                    }
                }
                else
                {
                    Debug.LogWarning($"Free High-Res TTS Download Error: {www.error}");
                }
            }
        }

        private IEnumerator PlayTauntLaughSequence()
        {
            // 깐족거리며 비웃는 "낄-낄-낄-낄" 4연타 억양 생성
            for (int k = 0; k < 4; k++)
            {
                int sampleRate = 44100;
                float duration = 0.14f;
                int samples = (int)(sampleRate * duration);
                float[] data = new float[samples];

                float laughFreq = 380f + (k % 2 == 0 ? 60f : 0f);

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Sin((t / duration) * Mathf.PI);
                    float wave = Mathf.Sin(2f * Mathf.PI * laughFreq * t);
                    float harm = Mathf.Sin(2f * Mathf.PI * laughFreq * 2.2f * t) * 0.5f;
                    data[i] = (wave + harm) * env * 0.7f;
                }

                AudioClip clip = AudioClip.Create($"Taunt_{k}", samples, 1, sampleRate, false);
                clip.SetData(data, 0);

                voiceSource.pitch = 1.1f + Random.Range(-0.1f, 0.15f);
                voiceSource.PlayOneShot(clip, 0.9f);
                yield return new WaitForSeconds(0.16f);
            }
        }

        private void PlayFanfareSFX()
        {
            StartCoroutine(FanfareCoroutine());
        }

        private IEnumerator FanfareCoroutine()
        {
            float[] notes = new float[] { 261.63f, 329.63f, 392.00f, 523.25f }; // C E G C (화려한 빵빠레 화음)
            foreach (float freq in notes)
            {
                int sampleRate = 44100;
                float duration = 0.25f;
                int samples = (int)(sampleRate * duration);
                float[] data = new float[samples];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Exp(-t * 6f);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.3f;
                    data[i] = tone * env * 0.5f;
                }

                AudioClip clip = AudioClip.Create($"Fanfare_{freq}", samples, 1, sampleRate, false);
                clip.SetData(data, 0);
                sfxSource.PlayOneShot(clip, 0.7f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        #endregion
    }
}
