using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Threading.Tasks;
using System;

using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using TMPro;


public class SaveImages : MonoBehaviour
{
    [Header("References")]
    public PythonClassifier pythonClient;
    public AudioSource audioSource;
    public AudioClip audioClip;
    public FeedbackHandler feedbackHandler;
    public TextMeshProUGUI setText;
    public Animator avatarAnimator;
    public Reset reset;
    public GameObject startButton;

    [Header("Settings")]
    [SerializeField] private float captureDelay;
    [SerializeField] private float setDuration;
    //[SerializeField] private float feedbackDuration = 18f;
    [SerializeField] private int nbrOfSets;
    [SerializeField] private bool saveImages;
    [SerializeField] private float waitTimeWhenDone;

    // Internal State
    private RawImage receivingRawImage;
    private float timer = 0f;
    private float streamTime = 0f;
    private bool streamActive = false;
    private bool stopped = false;
    private int sets = 0;

    public void Awake()
    {
        Texture.allowThreadedTextureCreation = true;
        setText.text = $"Sets: {sets}/{nbrOfSets}";
    }

    public void Update()
    {
        receivingRawImage = GetComponentInChildren<RawImage>();
        if (receivingRawImage != null && streamActive)
        {
            // Play countdown sound once at the start of streaming
            if (streamTime == 0f)
            {
                playCountdown();
            }

            streamTime += Time.deltaTime;

            // Start capturing images after initial 3 second delay
            if (streamTime > 3.0f && streamTime <= setDuration + 3.0f)
            {
                avatarAnimator.SetBool("InSet", true);
                timer += Time.deltaTime;
                InitializeImageAccess();
            }

            // Play end countdown sound once when streaming is about to end
            if (streamTime > setDuration && !stopped)
            {
                playCountdown();
                stopped = true;
                sets += 1;
            }
            else if (streamTime > setDuration + 3.0f && sets < nbrOfSets)
            {
                streamActive = false;
                avatarAnimator.SetBool("InSet", false);
                streamTime = 0f;
                stopped = false;
                setText.text = $"Sets: {sets}/{nbrOfSets}";
                feedbackHandler.handleFeedback(pythonClient.previous_classifiers);
                pythonClient.reset_classifiers();
            }
            // Reset stopped flag for next stream
            else if (streamTime > setDuration + 3.0f && sets >= nbrOfSets)
            {
                setText.text = $"Sets: {sets}/{nbrOfSets}";
                sets = 0;
                streamActive = false;
                avatarAnimator.SetBool("InSet", false);
                streamTime = 0f;
                stopped = false;
                
                feedbackHandler.handleFeedback(pythonClient.previous_classifiers);
                pythonClient.reset_classifiers();
                StartCoroutine(waitAndReset());
            }
        }
    }

    public void InitializeImageAccess()
    {
        if (receivingRawImage.mainTexture != null)
        {
            UnityEngine.Debug.Log($"Successfully found RawImage: {receivingRawImage.name}");

            if (timer >= captureDelay && receivingRawImage.mainTexture.width > 100 && receivingRawImage.mainTexture.height > 100)
            {
                timer = 0f;
                Texture2D tex = GetTexture2D(receivingRawImage.mainTexture);
                UnityEngine.Debug.Log("In timer");

                if (tex != null)
                {
                    byte[] bytes = ImageConversion.EncodeToJPG(tex, 25);

                    // Code for saving images locally for testing purposes
                    if (saveImages)
                    {
                        string folderPath = @"C:\Users\noren\OneDrive\Bilder\AR_Images";
                        string fileName = $"{System.DateTime.Now.ToString("yyyyMMdd_HHmmss")}.png";
                        string fullPath = Path.Combine(folderPath, fileName);

                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        File.WriteAllBytes(fullPath, bytes);
                        UnityEngine.Debug.Log("Image saved to " + fullPath);
                    }

                    string classificationResult = Convert.ToBase64String(bytes);
                    pythonClient.SendStringMessage(classificationResult);
                    UnityEngine.Debug.Log("Tex not null");

                    Destroy(tex);
                }
            }
        }
        else
        {
            UnityEngine.Debug.Log("RawImage child not found! Make sure it has been initialized.");
        }
    }

    private Texture2D GetTexture2D(Texture sourceTexture)
    {
        // Get dimensions
        int width = sourceTexture.width;
        int height = sourceTexture.height;

        // 1. Create a temporary RenderTexture to hold the source if it's not already one
        RenderTexture rt = RenderTexture.GetTemporary(width, height);

        // 2. Blit (copy) the source texture into the temporary RenderTexture
        Graphics.Blit(sourceTexture, rt);

        // 3. Store the currently active RenderTexture so we can restore it later
        RenderTexture previousActiveRT = RenderTexture.active;

        // 4. Set the temporary RenderTexture as the active target for rendering (reading)
        RenderTexture.active = rt;

        // 5. Create the new, readable Texture2D object
        Texture2D readableTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

        // 6. Read the pixels from the GPU (the active RenderTexture) into the CPU-side Texture2D
        readableTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        readableTexture.Apply();

        // 7. Clean up: Restore the original active RenderTexture and release the temporary one
        RenderTexture.active = previousActiveRT;
        RenderTexture.ReleaseTemporary(rt);

        return readableTexture;
    }

    public void handlePress()
    {
        streamActive = true;
        UnityEngine.Debug.Log("Server started!!!");
    }

    public void playCountdown()
    {
        audioSource.PlayOneShot(audioClip);
    }

    IEnumerator waitAndReset()
    {
        yield return new WaitForSeconds(waitTimeWhenDone);
        reset.initiateReset();
    }

    //private void handleFeedback()
    //{
    //    List<int> feedbackList = pythonClient.previous_classifiers;
    //    int lower_good = feedbackList.Count(x => x == 0);
    //    int middle_good = feedbackList.Count(x => x == 1);
    //    int upper_good = feedbackList.Count(x => x == 2);
    //    int left_bad = feedbackList.Count(x => x == 3);
    //    int right_bad = feedbackList.Count(x => x == 4);
    //    int wide_bad = feedbackList.Count(x => x == 5);
    //    bool anyBad = false;

    //    string result = "";
    //    if (left_bad >= 10)
    //    {
    //        result = result + "Leaning left. ";
    //        anyBad = true;
    //    }
    //    if (right_bad >= 10)
    //    {
    //        result = result + "Leaning right. ";
    //        anyBad = true;
    //    }
    //    if (wide_bad >= 10)
    //    {
    //        result = result + "Wide form. ";
    //        anyBad = true;
    //    }
    //    if (lower_good < 15 && !anyBad)
    //    {
    //        result = result + "Lower bad. ";
    //    }
    //    if (upper_good < 15 && !anyBad)
    //    {
    //        result = result + "Upper bad. ";
    //    }

    //    UnityEngine.Debug.Log($"Feedback: {result}");
    //}
}