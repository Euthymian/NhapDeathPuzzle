using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public Button againButton;

    void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show(int score, System.Action onAgain)
    {
        scoreText.text = score.ToString();
        gameObject.SetActive(true);
        againButton.onClick.RemoveAllListeners();
        againButton.onClick.AddListener(() => {
            gameObject.SetActive(false);
            onAgain?.Invoke();
        });
    }

    public void Hide() => gameObject.SetActive(false);
}
