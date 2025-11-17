AQUAGUARDIAN - PYTHON REGRESSION ANALYSIS
==========================================

INSTALLATION:
-------------
pip install pandas numpy matplotlib scipy

USAGE:
------
python analyze_trials.py

INPUT:
------
File: Trial_5_runs_.csv
Columns: trial_id, speed, verticalSpeed, idleUpwardSpeed, lifeTime,
         downHealthPairSec, removeHealthWithCollide, timeBetweenCollides,
         healHealthPoint, factor_force, o2_run1, o2_run2, ...

OUTPUT:
-------
1. RegressionResults/correlation_plot.png - Bar charts
2. RegressionResults/oxygen_trends.png - Trend lines across runs
3. RegressionResults/python_analysis_report.txt - Full analysis

CONFIGURATION:
--------------
Edit in analyze_trials.py:
- TARGET_OXYGEN_MIN = 1.0
- TARGET_OXYGEN_MAX = 5.0
- DATA_FILE = "Trial_5_runs_.csv"
- RESULTS_FOLDER = "RegressionResults"

FEATURES:
---------
- Pearson correlation analysis
- Statistical comparisons (ANOVA)
- Automatic recommendations
- Visual plots
- Detailed reports

