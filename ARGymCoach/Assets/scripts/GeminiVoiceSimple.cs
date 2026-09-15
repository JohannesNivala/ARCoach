using UnityEngine;
using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;


[Serializable]
public class STTResponse
{
    public STTResult[] results;
}

[Serializable]
public class STTResult
{
    public STTAlternative[] alternatives;
}

[Serializable]
public class STTAlternative
{
    public string transcript;
}


[Serializable]
public class GeminiCandidate
{
    public GeminiContent content;
}

[Serializable]
public class GeminiContent
{
    public GeminiPart[] parts;
}

[Serializable]
public class GeminiPart
{
    public string text;
}

[Serializable]
public class GeminiResponse
{
    public GeminiCandidate[] candidates;
}


[Serializable]
public class TTSResponse
{
    public string audioContent;
}


public class GeminiVoiceSimple : MonoBehaviour
{
    public string GEMINI_API_KEY = "";
    public string GOOGLE_API_KEY = "";
    public string prompt = "The answer you give should consist of 2 short sentences. " +
            "The sentences will consist of a kind desciption of what the user did wrong when doing shoulder presses according to what we will tell you and also how to fix it.";
    public string good_prompt = "The answer you give should only consist of 2 short sentence. " + 
        "The sentences will consist of compliments of how well the user seems to have performed the shoulder press exercise it just did and perhaps specify how they seemed to have good form and went all the way up and down with their arms during the exercise.";
    public string[] feedback_prompts;

    private string complete_feedback = "";

    AudioClip recording;
    bool isRecording = false;
    const int sampleRate = 16000;

    //void Update()
    //{
    //    if (Input.GetKeyUp(KeyCode.R))
    //        complete_feedback = good_prompt;
    //        _ = GetFeedback();
    //}

public void Send(string message)
    {
        complete_feedback += prompt;
        if (message.Contains("0"))
        {
            complete_feedback += feedback_prompts[0];
        }
        if (message.Contains("1"))
        {
            complete_feedback += feedback_prompts[1];
        }
        if (message.Contains("2"))
        {
            complete_feedback += feedback_prompts[2];
        }
        if (message.Contains("3"))
        {
            complete_feedback += feedback_prompts[3];
        }
        if (message.Contains("4"))
        {
            complete_feedback += feedback_prompts[4];
        }
        if (complete_feedback == prompt)
        {
            complete_feedback = good_prompt;
        }
        _ = GetFeedback();
    }

async Task GetFeedback()
    {
    // -------------------------
    //  GEMINI
    // -------------------------
    Debug.Log("⏳ Sending to Gemini...");
    string aiText = await GeminiGenerate(complete_feedback);
    complete_feedback = "";
    Debug.Log("AI: " + aiText);

    if (string.IsNullOrEmpty(aiText))
    {
        Debug.LogWarning("⚠️ Gemini returned empty response.");
        return;
    }

    // -------------------------
    //  TTS
    // -------------------------
    Debug.Log("⏳ Converting to TTS...");
    string audioB64 = await GoogleTTS(aiText);

    if (string.IsNullOrEmpty(audioB64))
    {
        Debug.LogError("❌ TTS RETURNED EMPTY AUDIO!");
        return;
    }

    // -------------------------
    //  PLAYBACK
    // -------------------------
    PlayBase64Wav(audioB64);
}



    // ======================================================
    // GOOGLE SPEECH-TO-TEXT
    // ======================================================

    async Task<string> GoogleSTT(string wavB64)
    {
        var body = new
        {
            config = new { encoding = "LINEAR16", sampleRateHertz = 16000, languageCode = "en-US" },
            audio = new { content = wavB64 }
        };

        using var client = new HttpClient();
        var res = await client.PostAsync(
            "https://speech.googleapis.com/v1/speech:recognize?key=" + GOOGLE_API_KEY,
            new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        );

        string json = await res.Content.ReadAsStringAsync();
        var stt = JsonConvert.DeserializeObject<STTResponse>(json);

        if (stt?.results != null && stt.results.Length > 0)
            return stt.results[0].alternatives[0].transcript;

        return "";
    }


    // ======================================================
    // GOOGLE TTS
    // ======================================================

    async Task<string> GoogleTTS(string text)
    {
        var body = new
        {
            input = new { text = text },
            voice = new { languageCode = "en-US", name = "en-US-Neural2-F" },
            audioConfig = new { audioEncoding = "LINEAR16" }
        };

        Debug.Log("TTS REQUEST: " + JsonConvert.SerializeObject(body));

        using var client = new HttpClient();
        var res = await client.PostAsync(
            "https://texttospeech.googleapis.com/v1/text:synthesize?key=" + GOOGLE_API_KEY,
            new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        );

        string json = await res.Content.ReadAsStringAsync();
        Debug.Log("TTS RAW RESPONSE: " + json);

        var tts = JsonConvert.DeserializeObject<TTSResponse>(json);

        if (tts == null || string.IsNullOrEmpty(tts.audioContent))
        {
            Debug.LogError("❌ TTS FAILED — audioContent is EMPTY");
            return "";
        }

        return tts.audioContent;
    }



    // ======================================================
    // GEMINI GENERATION (TEXT)
    // ======================================================

    async Task<string> GeminiGenerate(string text)
    {
        using var client = new HttpClient();

        var body = new
        {
            contents = new[]
            {
                new {
                    parts = new[]
                    {
                        new { text = text }
                    }
                }
            }
        };

        var jsonBody = JsonConvert.SerializeObject(body);

        var res = await client.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/gemma-3-4b-it:generateContent?key={GEMINI_API_KEY}",
            new StringContent(jsonBody, Encoding.UTF8, "application/json")
        );
        /*var res = await client.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={GEMINI_API_KEY}",
            new StringContent(jsonBody, Encoding.UTF8, "application/json")
        );*/

        string json = await res.Content.ReadAsStringAsync();
        Debug.Log("GEMINI RAW RESPONSE: " + json);

        if (string.IsNullOrEmpty(json))
            return "(EMPTY RESPONSE)";

        var gem = JsonConvert.DeserializeObject<GeminiResponse>(json);

        return gem.candidates[0].content.parts[0].text;
    }




    // ======================================================
    // AUDIO PLAYBACK
    // ======================================================

    void PlayBase64Wav(string base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            Debug.LogError("❌ No audio returned from TTS!");
            return;
        }

        byte[] bytes = Convert.FromBase64String(base64);
        Debug.Log("🎧 WAV length bytes = " + bytes.Length);

        if (bytes.Length < 50)
        {
            Debug.LogError("❌ Invalid WAV data");
            return;
        }

        int sampleRate = BitConverter.ToInt32(bytes, 24);   // from WAV header
        Debug.Log("🎧 WAV SampleRate detected = " + sampleRate);

        int headerSize = 44;
        int dataSize = bytes.Length - headerSize;
        int sampleCount = dataSize / 2;

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short s = BitConverter.ToInt16(bytes, headerSize + (i * 2));
            samples[i] = s / 32768f;
        }

        AudioClip clip = AudioClip.Create("GeminiSpeech", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);

        GetComponent<AudioSource>().PlayOneShot(clip);

        Debug.Log("▶️ Playing audio (" + sampleCount + " samples)");
    }




    // ======================================================
    // WAV ENCODER
    // ======================================================

    byte[] EncodeWAV(float[] samples, int sampleRate)
    {
        int byteCount = samples.Length * 2;
        byte[] wav = new byte[44 + byteCount];

        Buffer.BlockCopy(Encoding.ASCII.GetBytes("RIFF"), 0, wav, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(36 + byteCount), 0, wav, 4, 4);
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("WAVEfmt "), 0, wav, 8, 8);
        Buffer.BlockCopy(BitConverter.GetBytes(16), 0, wav, 16, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, wav, 20, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, wav, 22, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(sampleRate), 0, wav, 24, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(sampleRate * 2), 0, wav, 28, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((short)2), 0, wav, 32, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, wav, 34, 2);
        Buffer.BlockCopy(Encoding.ASCII.GetBytes("data"), 0, wav, 36, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(byteCount), 0, wav, 40, 4);

        int offset = 44;
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)(samples[i] * 32767f);
            wav[offset++] = (byte)(s & 0xff);
            wav[offset++] = (byte)((s >> 8) & 0xff);
        }

        return wav;
    }
}
