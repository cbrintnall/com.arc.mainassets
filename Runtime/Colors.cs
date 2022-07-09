using UnityEngine;

namespace Arc.Lib.Colors 
{
  public static class Colors
  {
    public static Color ToColor(this bool b) => b ? Color.green : Color.red;
  }
}