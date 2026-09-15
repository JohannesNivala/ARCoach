using UnityEngine;
using UnityEngine.SceneManagement;

public class Reset : MonoBehaviour
{
    [Header("References")]
    public GameObject avatar;
    public GameObject videoCanvas;
    public GameObject startSetCanvas;
    public GameObject setTextCanvas;
    public GameObject resetCanvas;
    public GameObject resetButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void initiateReset()
    {
        avatar.SetActive(false);
        videoCanvas.SetActive(false);
        startSetCanvas.SetActive(false);
        setTextCanvas.SetActive(false);
        resetCanvas.SetActive(true);
        resetButton.SetActive(true);
    }

    public void resetScene()
    {
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }
}
