using UnityEngine;
using TMPro;


public class InstructionsManager : MonoBehaviour
{

    [Header("References")]
    public TextMeshProUGUI instructions;
    public TextMeshProUGUI buttonText;
    public GameObject instructionCanvas;
    public GameObject instructionButtonNext;
    public GameObject instructionButtonPrevious;
    public GameObject setButton;
    public GameObject avatar;

    [Header("Settings")]
    public string[] instruction_list;
    public int clickDelay;

    private int instruction_step = -1;
    private bool hasClicked = false;
    private bool isFirstClick = true;
    private float clickTimer = 0f;

    private void Update()
    {
        Debug.Log(clickTimer);
        if (hasClicked)
        {
            clickTimer += Time.deltaTime;
            if (clickTimer > clickDelay)
            {
                clickTimer = 0f;
                hasClicked = false;
            }
        }     
    }

    public void nextStep()
    {
        if (!hasClicked)
        {
            hasClicked = true;
            instruction_step += 1;
            handleStep();
            if (instruction_step == 1)
            {
                instructionButtonNext.transform.position += new Vector3(0.2f, 0f, 0f);
                instructionButtonPrevious.SetActive(true);
            }
        }
        
    }

    public void previousStep()
    {
        if (!hasClicked)
        {
            hasClicked = true;
            instruction_step -= 1;
            handleStep();
            if (instruction_step < 1)
            {
                instructionButtonPrevious.SetActive(false);
                instructionButtonNext.transform.position += new Vector3(-0.2f, 0f, 0f);
            }
            if (buttonText.text == "DONE")
            {
                buttonText.text = "NEXT";
            }
        }
        
    }

    private void handleStep()
    {
        if (isFirstClick)
        {
            isFirstClick = false;
            instructions.fontSize = 6f;
        }
        if (instruction_step == instruction_list.Length - 1)
        {
            buttonText.text = "DONE";
        }
        if (instruction_step == instruction_list.Length)
        {
            setButton.SetActive(true);
            avatar.SetActive(true);
            instructionButtonNext.SetActive(false);
            instructionButtonPrevious.SetActive(false);
            instructionCanvas.SetActive(false);
        }
        else
        {
            instructions.text = instruction_list[instruction_step];
        }
    }
}
