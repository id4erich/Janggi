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

        // 설정 시트의 효과음·음성 대사 켜기/끄기 (기기에 저장)
        public const string KeySfx = "Janggi_Sfx", KeyVoice = "Janggi_Voice", KeyVibration = "Janggi_Vibration";

        public bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(KeyVibration, 1) == 1;
            set { PlayerPrefs.SetInt(KeyVibration, value ? 1 : 0); PlayerPrefs.Save(); }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
#endif

        // 짧은 진동 (Handheld.Vibrate는 길이를 못 정해 기기 Vibrator를 직접 씀)
        public void Vibrate(long ms)
        {
            if (!VibrationEnabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (vibrator == null)
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrator?.Call("vibrate", ms);
            }
            catch (System.Exception e) { Debug.LogWarning($"진동 실패: {e.Message}"); }
#endif
        }

        public bool SfxEnabled
        {
            get => !sfxSource.mute;
            set { sfxSource.mute = !value; PlayerPrefs.SetInt(KeySfx, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public bool VoiceEnabled
        {
            get => !voiceSource.mute;
            set
            {
                voiceSource.mute = !value; // 합성 목소리·웃음소리
                PlayerPrefs.SetInt(KeyVoice, value ? 1 : 0);
                PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!value && tts != null) tts.Call<int>("stop");
#endif
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitAudioSources();
#if UNITY_ANDROID && !UNITY_EDITOR
                InitTextToSpeech();
#endif
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

            sfxSource.mute = PlayerPrefs.GetInt(KeySfx, 1) == 0;
            voiceSource.mute = PlayerPrefs.GetInt(KeyVoice, 1) == 0;

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
            Vibrate(20);
        }

        /// <summary>
        /// 상대 말을 잡아챌 때의 강렬한 팍! 착수음
        /// </summary>
        public void PlayCaptureSound()
        {
            Vibrate(45);
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
            Vibrate(120);
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
        /// 한국어 음성 대사. Android는 기기 내장 TextToSpeech(오프라인)로 읽고, 에디터·기타 플랫폼은 로그만 남김
        /// </summary>
        public void SpeakFreeTTS(string textToSay) => SpeakComicTTS(textToSay, 1.0f);

        /// <summary>
        /// 코믹하고 깐족거리는 하이피치/헬륨 효과 한국어 음성
        /// </summary>
        public void SpeakComicTTS(string textToSay, float pitchMultiplier = 1.35f)
        {
            if (string.IsNullOrEmpty(textToSay) || !VoiceEnabled) return;
            // TTS가 '~' 문자를 '물결표'라고 읽지 않도록 필터링 처리
            string cleanText = textToSay.Replace("~", "").Replace("^", "").Replace("  ", " ");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!ttsReady)
            {
                // 앱 시작 직후처럼 초기화 전이면 마지막 대사 하나만 기억했다가 준비되면 읽음
                pendingText = cleanText;
                pendingPitch = pitchMultiplier;
                return;
            }
            SpeakNow(cleanText, pitchMultiplier);
#else
            Debug.Log($"[TTS] {cleanText}");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject tts;
        private bool ttsReady;
        private string pendingText;
        private float pendingPitch = 1f;

        private void SpeakNow(string text, float pitch)
        {
            try
            {
                tts.Call<int>("setPitch", pitch);
                using (var bundle = new AndroidJavaObject("android.os.Bundle"))
                    tts.Call<int>("speak", text, 0 /* QUEUE_FLUSH */, bundle, "janggi");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"TextToSpeech 재생 실패: {e.Message}");
            }
        }
        private volatile int ttsInitStatus = int.MinValue; // Android UI 스레드의 onInit 결과, Update에서 처리

        private class TtsInitListener : AndroidJavaProxy
        {
            private readonly JanggiSoundManager owner;
            public TtsInitListener(JanggiSoundManager owner) : base("android.speech.tts.TextToSpeech$OnInitListener") { this.owner = owner; }
            [UnityEngine.Scripting.Preserve] // Java가 이름으로 호출하므로 IL2CPP 스트리핑에서 보존
            public void onInit(int status) => owner.ttsInitStatus = status;
        }

        private void InitTextToSpeech()
        {
            tts = new AndroidJavaObject("android.speech.tts.TextToSpeech",
                UnityEngine.Android.AndroidApplication.currentActivity, new TtsInitListener(this));
        }

        private void Update()
        {
            if (ttsReady || ttsInitStatus == int.MinValue || tts == null) return;
            int status = ttsInitStatus;
            ttsInitStatus = int.MinValue;
            if (status != 0) { Debug.LogWarning($"TextToSpeech 초기화 실패: {status}"); return; }

            int lang;
            using (var korean = new AndroidJavaObject("java.util.Locale", "ko", "KR"))
                lang = tts.Call<int>("setLanguage", korean);
            // LANG_MISSING_DATA(-1)·LANG_NOT_SUPPORTED(-2)면 엉뚱한 언어로 읽지 않도록 대사를 끔
            if (lang < 0) { Debug.LogWarning($"TextToSpeech 한국어 미지원: {lang}"); return; }
            ttsReady = true;
            if (pendingText != null)
            {
                SpeakNow(pendingText, pendingPitch);
                pendingText = null;
            }
        }

        private void OnDestroy()
        {
            if (Instance != this || tts == null) return;
            tts.Call("shutdown");
            tts.Dispose();
        }
#endif

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
