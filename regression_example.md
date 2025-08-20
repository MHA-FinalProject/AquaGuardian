# מדריך לשימוש ברגרסיה ליניארית ל-AquaGuardian

## מטרה
כיול אוטומטי של פרמטרי קושי כדי שהחמצן בסוף המשחק יהיה כמה שיותר קרוב ל-0 אבל חיובי.

## איך זה עובד

### 1. איסוף נתונים
המערכת יוצרת קובץ CSV עם הנתונים הבאים לכל סשן:

**פיצ'רים (קלט X):**
- `O2_Start`: חמצן התחלתי (בדרך כלל 100%)
- `O2_Rate_Per_Second`: כמה חמצן יורד כל שנייה
- `O2_Rate_Per_Collision`: כמה חמצן יורד בכל התנגשות
- `Total_Cave_Count`: מספר המערות
- `Total_Length`: סכום אורכי המערות
- `Avg_Diameter`: ממוצע קטרי המערות
- `Avg_Height`: ממוצע גבהי המערות
- `Total_Estimated_Time`: זמן משוער כולל

**יעד (פלט y):**
- `Final_O2_Percent`: חמצן בסוף המשחק

### 2. אימון המודל
```python
# בפייתון
from sklearn.linear_model import LinearRegression
import pandas as pd

# טעינת נתונים
df = pd.read_csv('regression_data_20250117_143228.csv')

# הגדרת X ו-y
X = df[['O2_Start', 'O2_Rate_Per_Second', 'O2_Rate_Per_Collision', 
        'Total_Cave_Count', 'Total_Length', 'Avg_Diameter', 
        'Avg_Height', 'Total_Estimated_Time']]
y = df['Final_O2_Percent']

# אימון
model = LinearRegression().fit(X, y)
```

### 3. חיזוי וכיול
לאחר אימון, המודל נותן משוואה:
```
Final_O2 = β0 + β1×O2_Start + β2×O2_Rate_Per_Second + β3×O2_Rate_Per_Collision + ...
```

לכיול `O2_Rate_Per_Second` לחמצן יעד (למשל 3%):
```python
def solve_optimal_rate_per_second(target_o2=3.0, **known_params):
    β0 = model.intercept_
    βs = dict(zip(feature_names, model.coef_))
    
    # חישוב הקצב האופטימלי
    numerator = target_o2 - β0
    for param, value in known_params.items():
        if param != 'O2_Rate_Per_Second':
            numerator -= βs[param] * value
    
    return numerator / βs['O2_Rate_Per_Second']

# דוגמה
optimal_rate = solve_optimal_rate_per_second(
    target_o2=3.0,
    O2_Start=100.0,
    O2_Rate_Per_Collision=5.0,
    Total_Cave_Count=5,
    Total_Length=50.0,
    Avg_Diameter=3.0,
    Avg_Height=2.5,
    Total_Estimated_Time=60.0
)
print(f"קצב אופטימלי: {optimal_rate:.4f}% לשנייה")
```

## איך להשתמש במערכת

### שלב 1: הפעלת הייצוא
1. פתחי את Unity
2. לחצי על `Tools → Export Regression Data` בתפריט העליון
3. הקובץ יישמר ב-`Assets/RegressionData/`

### שלב 2: אימון בפייתון
1. העתיקי את הקובץ CSV למחשב שלך
2. הריצי את הסקריפט Python שנוצר
3. קבלי את המקדמים β

### שלב 3: הכנסה ל-Unity
```csharp
// ב-Unity, צרי DifficultyModel עם המקדמים
[CreateAssetMenu(fileName = "DifficultyModel", menuName = "AquaGuardian/Difficulty Model")]
public class DifficultyModelSO : ScriptableObject
{
    [Header("Linear Regression Coefficients")]
    public float β0 = 0f; // intercept
    public float β1 = 0f; // O2_start
    public float β2 = 0f; // O2_rate_per_second
    public float β3 = 0f; // O2_rate_per_collision
    public float β4 = 0f; // total_cave_count
    public float β5 = 0f; // total_length
    public float β6 = 0f; // avg_diameter
    public float β7 = 0f; // avg_height
    public float β8 = 0f; // total_estimated_time
    
    public float SolveOptimalO2Rate(float targetO2, 
                                   float o2Start, float o2RatePerCollision,
                                   int totalCaves, float totalLength,
                                   float avgDiameter, float avgHeight,
                                   float totalEstimatedTime)
    {
        if (Mathf.Abs(β2) < 1e-6f) return 1.0f; // fallback
        
        float numerator = targetO2 - (β0 + β1*o2Start + β3*o2RatePerCollision +
                                     β4*totalCaves + β5*totalLength + 
                                     β6*avgDiameter + β7*avgHeight + β8*totalEstimatedTime);
        return numerator / β2;
    }
}
```

### שלב 4: שימוש אוטומטי
```csharp
// ב-GameManager או PanelOpenUp
public DifficultyModelSO difficultyModel;

void SetupOptimalDifficulty()
{
    if (difficultyModel == null) return;
    
    // חישוב פרמטרי הזירה הנוכחית
    float totalLength = CalculateTotalCaveLength();
    float avgDiameter = CalculateAverageCaveDiameter();
    // ... שאר הפרמטרים
    
    // חישוב קצב חמצן אופטימלי
    float optimalRate = difficultyModel.SolveOptimalO2Rate(
        targetO2: 3.0f, // יעד: 3% חמצן נותר
        o2Start: 100f,
        o2RatePerCollision: 5f,
        totalCaves: caveInfos.Count,
        totalLength: totalLength,
        avgDiameter: avgDiameter,
        avgHeight: avgHeight,
        totalEstimatedTime: estimatedTime
    );
    
    // עדכון הגדרות המשחק
    var health = FindObjectOfType<Health>();
    if (health != null)
    {
        // עדכן את הגדרות החמצן
        // health.SetO2Rate(optimalRate);
    }
}
```

## יתרונות הגישה
1. **אוטומטי**: לא צריך לנחש פרמטרים
2. **מדויק**: מבוסס על נתונים אמיתיים
3. **מסתגל**: משתפר עם עוד נתוני אימון
4. **מהיר**: חישוב מהיר של פרמטרים חדשים

## המלצות
1. **איסוף נתונים**: הריצי 10-15 סשנים עם פרמטרים שונים לפני אימון המודל
2. **אימות**: אחרי אימון, בדקי 2-3 סשנים עם הפרמטרים החדשים
3. **עדכון**: כל כמה שבועות, הוסיפי נתונים חדשים ואמני מחדש
4. **יעד**: התחילי עם יעד של 2-5% חמצן נותר, ותתאימי לפי הצורך

## קבצים שנוצרים
- `Assets/RegressionData/regression_data_YYYYMMDD_HHMMSS.csv` - נתוני הרגרסיה
- `Assets/RegressionData/linear_regression_example.py` - סקריפט Python לאימון




