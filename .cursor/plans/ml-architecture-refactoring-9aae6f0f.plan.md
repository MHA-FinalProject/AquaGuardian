<!-- 9aae6f0f-015b-40f7-ad07-0554d95b7e88 2d724aac-34c8-4c74-9099-d2f38b55f076 -->
# ארגון מחדש של ארכיטקטורת ML - הפרדה נקייה

## מטרות

1. הפרדת אחריות ברורה בין מחלקות
2. שמות ברורים ומתאימים לכל מחלקה
3. מרכוז לוגיקת ML בשכבה אחת סגורה
4. מקור אמת יחיד לפיצ'רים
5. API פשוט וברור
6. שמירה על כל הלוגיקה הקיימת ללא שינויים

## מבנה חדש - שמות ברורים והפרדה נקייה

### 1. LinearRegressionModel.cs (חדש)

**תפקיד:** ליבת ML אחת - אימון, חזוי, וכל החישובים המתמטיים

**מקור:** מאחדת `MultipleLinearRegression.cs` + `RegressionMath.cs`

**אחריות:**

- אימון מודל רגרסיה לינארית עם Ridge regularization
- חזוי ערכים
- K-Fold Cross Validation
- חישובי מקדמים וחשיבות פיצ'רים
- נרמול/דה-נרמול פיצ'רים
- חישובי Beta אפקטיבי (chain rule ל-derived features)

**API:**

- `Fit(float[][] X, float[] y, string[] names)` - אימון מודל
- `Predict(float[] x)` - חזוי
- `KFoldCV(float[][] X, float[] y, int k)` - Cross-validation
- `GetCoefficients()` - קבלת מקדמים
- `GetFeatureImportance()` - חשיבות פיצ'רים
- `EffectiveBeta(int featureIndex, TrialDataModels.TrialData currentParams, bool optimizingDerivedDependencies)` - Beta אפקטיבי
- `SumBetaSq(int[] freeFeatures, TrialDataModels.TrialData currentParams)` - סכום Beta בריבוע
- `ToNormalized(int featureIndex, float rawValue)` - נרמול
- `FromNormalized(int featureIndex, float normalizedValue)` - דה-נרמול

**שינויים:**

- מאחדת את כל הלוגיקה המתמטית
- שומרת על כל הפונקציונליות הקיימת
- FeatureNormalizer נשאר חלק פנימי

### 2. TrialFeatureExtractor.cs (חדש, מחליף FeatureExtractor)

**תפקיד:** מקור אמת יחיד למיצוי פיצ'רים מניסיונות

**מקור:** מחליף `FeatureExtractor.cs`

**אחריות:**

- מיצוי פיצ'רים מניסיון יחיד או מרובים
- הגדרת שמות פיצ'רים (single source of truth)
- חישובי baseline ו-defaults
- טיפול ב-Amadeo/Keyboard (factorForce=0 אם Keyboard)

**API:**

- `ExtractFeatures(TrialDataModels.TrialData trial)` - מיצוי מניסיון יחיד
- `ExtractFeatures(List<TrialDataModels.TrialData> trials)` - מיצוי מניסיונות מרובים
- `ExtractFeaturesAndTargets(List<TrialDataModels.TrialData> trials)` - מיצוי פיצ'רים + יעדים
- `FeatureNames` - שמות הפיצ'רים (readonly)
- `FeatureCount` - מספר פיצ'רים
- `GetPatientBaseline(List<TrialDataModels.TrialData> trials, TrialDataModels.ParameterRanges ranges, bool useMedian)` - baseline אישי
- `GetMidRangeDefaults(TrialDataModels.ParameterRanges ranges)` - ערכי ברירת מחדל

**שינויים:**

- שמירה על כל הלוגיקה הקיימת (כולל factorForce=0 אם Keyboard)
- רק שינוי שם המחלקה להבהרת תפקיד

### 3. OxygenDifficultyTuner.cs (חדש)

**תפקיד:** חזאי חמצן + פתרון פרמטרים ליעד חמצן

**מקור:** מאחדת `OxygenPredictor.cs` + `DifficultyParameterSolver.cs`

**אחריות:**

- אימון מודל חזוי חמצן
- חזוי רמת חמצן מפרמטרים
- פתרון פרמטרים אופטימליים ליעד חמצן נתון
- בחירת פיצ'רים (feature selection)

**API:**

- `Train(List<TrialDataModels.TrialData> trials, bool enableFeatureSelection = false)` - אימון מודל
- `Predict(TrialDataModels.TrialData data)` - חזוי חמצן
- `SolveForTargetO2(float target, TrialDataModels.ParameterRanges ranges, List<TrialDataModels.TrialData> history, out float error)` - פתרון ליעד O2
- `GetFeatureImportance()` - חשיבות פיצ'רים
- `GetModel()` - קבלת המודל הפנימי (LinearRegressionModel)

**שינויים:**

- מאחדת את כל לוגיקת החזוי והאופטימיזציה
- משתמשת ב-ParameterOptimizationHelper לפתרון
- **לא כוללת** סטטיסטיקה (averageOxygen, perfectTrials וכו') - זה נשאר ב-TrialRegressionAlgorithm

### 4. ParameterOptimizationHelper.cs (מחלקת עזר חדשה)

**תפקיד:** אלגוריתמי אופטימיזציה טהורים לפתרון פרמטרים

**מקור:** חלקים מ-`DifficultyParameterSolver.cs` - האלגוריתמים הטהורים

**אחריות:**

- פתרון minimal-change (closed-form)
- Refinement עם projected gradient
- Refinement איטרטיבי

**API (סטטי):**

- `SolveMinimalChange(LinearRegressionModel model, TrialDataModels.TrialData baseParams, int[] freeFeatures, float targetO2, Vector2[] bounds)` - פתרון minimal-change
- `RefineProjectedGradient(LinearRegressionModel model, TrialDataModels.TrialData start, int[] freeFeatures, float targetO2, Vector2[] bounds, System.Func<TrialDataModels.TrialData, float> predictO2, int maxSteps, float lr, float tol)` - refinement עם gradient
- `RefineProjectedGradientIterative(LinearRegressionModel model, TrialDataModels.TrialData start, int[] freeFeatures, float targetO2, Vector2[] bounds, System.Func<TrialDataModels.TrialData, float> predictO2, int maxIterations)` - refinement איטרטיבי

**שינויים:**

- מחלקת עזר סטטית טהורה - רק אלגוריתמים
- מופרדת מ-OxygenDifficultyTuner לקריאות

### 5. TrialRegressionAlgorithm.cs (משופר)

**תפקיד:** Flow orchestrator - מנהל את תהליך הניתוח המלא

**אחריות:**

- תיאום בין כל המחלקות
- חישובי סטטיסטיקה (averageOxygen, perfectTrials, failedTrials)
- בניית דוחות
- ConstrainRangesToObserved (helper פנימי)

**שינויים:**

- מסיר כל לוגיקת ML פנימית
- משתמש ב-OxygenDifficultyTuner במקום OxygenPredictor + DifficultyParameterSolver
- משתמש ב-TrialFeatureExtractor במקום FeatureExtractor
- שומר על כל הסטטיסטיקה והדוחות

### 6. TrialRegressionUI.cs (משופר)

**תפקיד:** UI בלבד - כפתורים, טקסט, שמירה

**שינויים:**

- משתמש ב-TrialRegressionAlgorithm (אין שינוי ב-API)
- הסרת FindObjectOfType אם אפשר (dependency injection)

### 7. TrialParameterManager.cs (נשאר כפי שהוא)

**תפקיד:** טעינה/יישום/שמירה של פרמטרים

**שינויים:** אין - נשאר כמו שהוא

## קבצים לגיבוי (Backup)

כל הקבצים הישנים יישמרו עם סיומת `.backup.cs`:

- `MultipleLinearRegression.cs.backup.cs`
- `RegressionMath.cs.backup.cs`
- `FeatureExtractor.cs.backup.cs`
- `OxygenPredictor.cs.backup.cs`
- `DifficultyParameterSolver.cs.backup.cs`

## סדר ביצוע

1. יצירת מחלקות חדשות (LinearRegressionModel, TrialFeatureExtractor, OxygenDifficultyTuner, ParameterOptimizationHelper)
2. עדכון TrialRegressionAlgorithm להשתמש במחלקות החדשות
3. עדכון TrialRegressionUI (מינימלי)
4. בדיקת קומפילציה ותיקון שגיאות
5. גיבוי קבצים ישנים
6. מחיקת קבצים ישנים (אם הכל עובד)

## פרטים קריטיים - לוגיקה שצריך לשמור בדיוק

### 1. LinearRegressionModel - פרטים קריטיים

- **Ridge regularization:** שומר על adaptiveRidgeLambda לפי גודל dataset
- **Cholesky decomposition:** שומר על SolveSPDByCholesky בדיוק כפי שהוא
- **Normalization:** FeatureNormalizer נשאר חלק פנימי, שומר על ToNormalized/FromNormalized
- **Chain rule:** EffectiveBeta שומר על לוגיקת chain rule ל-derived features (EffectiveDrainRate)
- **NaN/Inf protection:** שומר על כל ה-CleanVector/CleanMatrix helpers
- **K-Fold CV:** שומר על לוגיקת fold size calculation (baseSize + remainder)
- **Feature importance:** שומר על חישוב לפי absolute coefficients

### 2. TrialFeatureExtractor - פרטים קריטיים

- **Amadeo/Keyboard logic:** 
- factorForce = 0 אם !isAmadeo (חייב!)
- idleUpwardSpeed * 0.5f אם isAmadeo (חייב!)
- **Feature names:** FeatureNames array נשאר זהה (10 features)
- **Baseline calculation:** 
- GetPatientBaseline: median/average logic בדיוק כפי שהוא
- GetMidRangeDefaults: mid-range calculation זהה
- **EffectiveDrainRate:** נשאר derived feature (index 9)

### 3. OxygenDifficultyTuner - פרטים קריטיים

- **Feature selection:** 
- Adaptive topK לפי גודל dataset
- Ridge lambda adaptive לפי n samples
- Validation logic (minR2, maxError) לפי dataset size
- **Predict:** Clamp ל-[0, 100]% כמו קודם
- **Training:** שומר על validation logic (variance check, R2 check)
- **SolveForTargetO2:** 
- ConstrainRangesToObserved לפני פתרון
- Smart baseline adjustment ל-low target oxygen
- Fallback ל-gradient descent אם advanced solver נכשל
- Skip factorForce אם כל trials הם keyboard-only

### 4. ParameterOptimizationHelper - פרטים קריטיים

- **SolveMinimalChange:**
- Fixed contribution calculation כולל skip derived features אם optimizing dependencies
- Chain rule handling דרך EffectiveBeta
- Normalized space calculation (xHat)
- Clamp ב-RAW space לפני החזרה
- **RefineProjectedGradient:**
- Adaptive learning rate לפי error
- Global beta normalization (sumBetaSq) - חשוב!
- Normalized space gradient updates
- **RefineProjectedGradientIterative:**
- Best params tracking
- Adaptive LR לפי absError
- Convergence check

### 5. TrialRegressionAlgorithm - פרטים קריטיים

- **ConstrainRangesToObserved:**
- Buffer calculation (15% random, 5% constant)
- ExpandRange עם clamp ל-original limits
- **Statistics calculation:**
- perfectTrials: target ± tolerance
- failedTrials: oxygen <= 0
- averageOxygen: sum / count
- **Report generation:**
- PrintCoefficientsRealTime עם compact format
- Feature importance top 5
- Optimization report building

### 6. Dependencies קריטיות

- **FeatureNormalizer:** נשאר מחלקה נפרדת, פנימית ל-LinearRegressionModel
- **ParameterHelper:** נשאר מחלקה נפרדת, משמש את כל המחלקות
- **TrialDataModels:** נשאר ללא שינוי
- **TrialReportGenerator:** נשאר ללא שינוי (משתמש ב-OxygenPredictor API)

## הערות חשובות

- כל הלוגיקה הקיימת נשמרת בדיוק כפי שהיא - לא משנים שום חישוב
- רק הפרדת אחריות וארגון מחדש - שינוי מבנה, לא לוגיקה
- API ציבורי נשאר זהה ככל האפשר - רק משנים איך קוראים למחלקות
- אין שינויים בלוגיקה מתמטית - כל החישובים זהים
- סטטיסטיקה נשארת ב-TrialRegressionAlgorithm - לא עוברת ל-DifficultyTuner
- שמות מחלקות ברורים ומתאימים לתפקידן
- כל הפונקציות הפנימיות נשמרות - גם אם הן private
- כל ה-helpers נשמרים - גם אם הם פנימיים
- כל המקרים המיוחדים נשמרים - Amadeo/Keyboard, derived features, validation logic

### To-dos

- [ ] יצירת LinearRegressionModel.cs - מאחדת MultipleLinearRegression + RegressionMath. לוודא: Ridge adaptive, Cholesky, Normalization, Chain rule (EffectiveBeta), NaN protection, K-Fold logic, Feature importance
- [ ] יצירת TrialFeatureExtractor.cs - מחליף FeatureExtractor. לוודא: Amadeo/Keyboard logic (factorForce=0, idleUpward*0.5), FeatureNames זהה, Baseline calculations (median/average), EffectiveDrainRate derived
- [ ] יצירת ParameterOptimizationHelper.cs - אלגוריתמי אופטימיזציה. לוודא: SolveMinimalChange (fixed contribution, chain rule, normalized space), RefineProjectedGradient (adaptive LR, global beta norm), RefineProjectedGradientIterative (best tracking)
- [ ] יצירת OxygenDifficultyTuner.cs - מאחדת OxygenPredictor + DifficultyParameterSolver. לוודא: Feature selection adaptive, Ridge adaptive, Validation logic, Predict clamp [0,100], SolveForTargetO2 (constrain ranges, smart baseline, fallback, skip factorForce if keyboard)
- [ ] עדכון TrialRegressionAlgorithm. לוודא: ConstrainRangesToObserved (buffer logic), Statistics (perfect/failed/average), PrintCoefficientsRealTime, Report generation, כל ה-helpers הפנימיים
- [ ] עדכון כל ה-references במערכת: TrialReportGenerator, וכל קבצים אחרים שמשתמשים ב-OxygenPredictor/FeatureExtractor/DifficultyParameterSolver
- [ ] עדכון TrialRegressionUI - מינימלי, רק אם צריך dependency injection
- [ ] גיבוי כל הקבצים הישנים עם סיומת .backup.cs: MultipleLinearRegression, RegressionMath, FeatureExtractor, OxygenPredictor, DifficultyParameterSolver
- [ ] בדיקת קומפילציה ותיקון שגיאות - לוודא שכל הלוגיקה עובדת