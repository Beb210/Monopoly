// AudioToggle.cs
// Отвечает за музыку в игре
using UnityEngine;
using UnityEngine.UI;

public class AudioToggle : MonoBehaviour
{
    public Sprite speakerOnSprite;
    public Sprite speakerOffSprite;
    public Button toggleButton;
    
    private bool isMuted = false;

    void Start()
    {
        // Подгружаем сохраненное состояние, чтобы при перезапуске игры звук не сбрасывался
        isMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
        UpdateAudioState();
        UpdateButtonIcon();
    }

    public void ToggleAudio()
    {
        isMuted = !isMuted;
        UpdateAudioState();
        UpdateButtonIcon();
        
        // Сохраняем состояние между сессиями
        PlayerPrefs.SetInt("AudioMuted", isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    void UpdateAudioState()
    {
        AudioListener.volume = isMuted ? 0f : 1f;
    }

    void UpdateButtonIcon()
    {
        if (toggleButton != null && toggleButton.image != null)
        {
            toggleButton.image.sprite = isMuted ? speakerOffSprite : speakerOnSprite;
        }
    }
}