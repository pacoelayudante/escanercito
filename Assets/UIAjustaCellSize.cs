using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIAjustaCellSize : MonoBehaviour
{    
    RectTransform rectTransform;
    RectTransform RectTransform => rectTransform?rectTransform:rectTransform=GetComponent<RectTransform>();

     private void OnEnable() {
         var grupoCells = GetComponent<GridLayoutGroup>();
        if (grupoCells.constraint == GridLayoutGroup.Constraint.FixedColumnCount) {
            grupoCells.cellSize = Vector2.one*RectTransform.rect.width/grupoCells.constraintCount;
        }
        else if (grupoCells.constraint == GridLayoutGroup.Constraint.FixedRowCount) {
            grupoCells.cellSize = Vector2.one*RectTransform.rect.height/grupoCells.constraintCount;
        }
    }
}
