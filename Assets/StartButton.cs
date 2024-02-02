using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class StartButton : MonoBehaviour
{
    public TextMeshProUGUI buttonText;
    bool loading = false;

    void OnParticleCollision(GameObject other) {
        LoadScene(other);
    }

    void OnCollisionEnter(Collision collision) {
        LoadScene(collision.gameObject);
    }

    void LoadScene(GameObject other) {
        // layer 9 is ship and particles
        if (!loading && other.layer == 9) {
            loading = true;
            buttonText.text = "Loading...";
            StartCoroutine(LoadAsyncScene());
        }
    }

    IEnumerator LoadAsyncScene()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Solar System", LoadSceneMode.Single);

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

}
