using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arc.Lib
{
  public class DevConsole : MonoBehaviour
  {
    #region singleton
    public static DevConsole instance
    {
      get
      {
        if (_instance == null)
        {
          GameObject console = new GameObject("__devconsole");
          GameObject.DontDestroyOnLoad(console);
          _instance = console.AddComponent<DevConsole>();
        }

        return _instance;
      }
    }

    private static DevConsole _instance;
    #endregion

    RawImage _background;

    private void Awake()
    {
      var canvas = gameObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;

      var uiBase = new GameObject("__devconsole_panel");
      uiBase.transform.SetParent(canvas.transform);
      _background = uiBase.AddComponent<RawImage>();

      Color baseColor = Color.black;
      baseColor.a = .3f;
      _background.color = baseColor;
      _background.rectTransform.anchorMin = Vector2.zero;
      _background.rectTransform.anchorMax = Vector2.one;
      _background.rectTransform.offsetMax = Vector2.zero;
      _background.rectTransform.offsetMin = Vector2.zero;
    }

    public void Write(string line)
    {

    }
  }
}