using UnityEngine;

public class fadein : MonoBehaviour
{
    public GameObject PanelUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void fade_in()
    {
        Debug.Log("fade_inが呼ばれた");
        PanelUI.SetActive(true);

    }
}
