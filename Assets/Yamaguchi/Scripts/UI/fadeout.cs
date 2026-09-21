using UnityEngine;

public class fadeout : MonoBehaviour
{
    public GameObject PanelUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public void fade_out()
    {
        PanelUI.SetActive(false);
    }
}
