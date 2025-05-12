using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Varwin;

public class LoaderFeedBackText : MonoBehaviour
{
    private Text _text;
    private TMP_Text _tmpText;

    private void Start()
    {
        _text = GetComponent<Text>();
        _tmpText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (LoaderAdapter.Loader == null)
        {
            return;
        }

        if (_text != null)
        {
            _text.text = LoaderAdapter.FeedBackText;
        }

        if (_tmpText != null)
        {
            _tmpText.text = LoaderAdapter.FeedBackText;
        }
    }
}