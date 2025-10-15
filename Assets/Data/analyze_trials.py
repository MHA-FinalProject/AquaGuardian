#!/usr/bin/env python3
"""
AquaGuardian Trial Regression Analysis
"""

import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy import stats
from pathlib import Path
import warnings
warnings.filterwarnings('ignore')

TARGET_OXYGEN_MIN = 1.0
TARGET_OXYGEN_MAX = 5.0
DATA_FILE = "Trial_5_runs_.csv"
RESULTS_FOLDER = "RegressionResults"

class TrialAnalyzer:
    
    def __init__(self, data_file=DATA_FILE):
        self.data_file = Path(data_file)
        self.df = None
        self.oxygen_columns = []
        self.parameter_columns = [
            'speed', 'verticalSpeed', 'idleUpwardSpeed', 'lifeTime',
            'downHealthPairSec', 'removeHealthWithCollide', 
            'timeBetweenCollides', 'healHealthPoint', 'factor_force'
        ]
        
    def load_data(self):
        print(f"Loading data from {self.data_file}...")
        
        if not self.data_file.exists():
            raise FileNotFoundError(f"File not found: {self.data_file}")
        
        self.df = pd.read_csv(self.data_file)
        self.oxygen_columns = [col for col in self.df.columns if col.startswith('o2_run')]
        
        if 'final_oxygen_remaining' in self.df.columns:
            self.oxygen_columns.append('final_oxygen_remaining')
        
        if not self.oxygen_columns:
            raise ValueError("No oxygen data columns found!")
        
        print(f"Loaded {len(self.df)} trials, {len(self.oxygen_columns)} run(s)")
        return self
    
    def calculate_correlations(self, oxygen_col):
        correlations = {}
        valid_data = self.df[self.df[oxygen_col].notna()].copy()
        
        if len(valid_data) < 2:
            return correlations
        
        for param in self.parameter_columns:
            if param in valid_data.columns:
                corr, p_value = stats.pearsonr(valid_data[param], valid_data[oxygen_col])
                correlations[param] = {
                    'correlation': corr,
                    'p_value': p_value,
                    'strength': self._get_strength(corr)
                }
        
        return correlations
    
    def _get_strength(self, corr):
        abs_corr = abs(corr)
        if abs_corr > 0.7:
            return 'STRONG'
        elif abs_corr > 0.3:
            return 'MODERATE'
        else:
            return 'WEAK'
    
    def analyze_run(self, oxygen_col):
        print(f"\n{'='*60}")
        print(f"ANALYZING: {oxygen_col}")
        print(f"{'='*60}")
        
        valid_data = self.df[self.df[oxygen_col].notna()].copy()
        oxygen_values = valid_data[oxygen_col].values
        
        print(f"\nOXYGEN STATISTICS:")
        print(f"   Trials Analyzed: {len(oxygen_values)}")
        print(f"   Average: {oxygen_values.mean():.1f}%")
        print(f"   Min: {oxygen_values.min():.1f}%  |  Max: {oxygen_values.max():.1f}%")
        
        perfect = sum(oxygen_values <= TARGET_OXYGEN_MAX)
        failed = sum(oxygen_values <= 0)
        print(f"   Perfect: {perfect}  |  Failed: {failed}")
        
        correlations = self.calculate_correlations(oxygen_col)
        
        if not correlations:
            return None
        
        sorted_corr = sorted(correlations.items(), 
                           key=lambda x: abs(x[1]['correlation']), 
                           reverse=True)
        
        print(f"\nTOP CORRELATIONS:")
        print(f"   {'Parameter':<25} {'Corr':<8} {'Strength':<12} {'P-value'}")
        print(f"   {'-'*60}")
        
        for param, data in sorted_corr[:5]:
            arrow = '+' if data['correlation'] > 0 else '-'
            print(f"   {arrow} {param:<23} {data['correlation']:>6.3f}  "
                  f"{data['strength']:<12} {data['p_value']:.4f}")
        
        print(f"\nRECOMMENDATIONS:")
        
        avg_oxygen = oxygen_values.mean()
        if failed > 0:
            print(f"   {failed} trials failed - REDUCE difficulty")
        elif avg_oxygen > 15:
            print(f"   Too much oxygen ({avg_oxygen:.1f}%) - INCREASE difficulty")
        elif TARGET_OXYGEN_MIN <= avg_oxygen <= 10:
            print(f"   Excellent balance")
        
        most_positive = max(correlations.items(), key=lambda x: x[1]['correlation'])
        most_negative = min(correlations.items(), key=lambda x: x[1]['correlation'])
        
        if abs(most_positive[1]['correlation']) > 0.3:
            print(f"   INCREASE {most_positive[0]} (r={most_positive[1]['correlation']:.2f})")
        if abs(most_negative[1]['correlation']) > 0.3:
            print(f"   DECREASE {most_negative[0]} (r={most_negative[1]['correlation']:.2f})")
        
        print(f"\n   Target: {TARGET_OXYGEN_MIN}-{TARGET_OXYGEN_MAX}%")
        
        return {
            'oxygen_col': oxygen_col,
            'stats': {
                'mean': oxygen_values.mean(),
                'std': oxygen_values.std(),
                'min': oxygen_values.min(),
                'max': oxygen_values.max(),
                'perfect': perfect,
                'failed': failed
            },
            'correlations': correlations
        }
    
    def plot_correlations(self, results, save=True):
        if not results:
            return
        
        print(f"\nCreating correlation plots...")
        
        fig, axes = plt.subplots(1, len(results), figsize=(6*len(results), 5))
        if len(results) == 1:
            axes = [axes]
        
        for idx, result in enumerate(results):
            correlations = result['correlations']
            params = list(correlations.keys())
            corr_values = [correlations[p]['correlation'] for p in params]
            
            colors = ['green' if c > 0 else 'red' for c in corr_values]
            axes[idx].barh(params, corr_values, color=colors, alpha=0.7)
            axes[idx].axvline(x=0, color='black', linestyle='-', linewidth=0.5)
            axes[idx].set_xlabel('Correlation Coefficient')
            axes[idx].set_title(f"{result['oxygen_col']}\n(Mean O2: {result['stats']['mean']:.1f}%)")
            axes[idx].grid(axis='x', alpha=0.3)
        
        plt.tight_layout()
        
        if save:
            output_file = Path(RESULTS_FOLDER) / 'correlation_plot.png'
            output_file.parent.mkdir(exist_ok=True)
            plt.savefig(output_file, dpi=300, bbox_inches='tight')
            print(f"Saved: {output_file}")
        
        plt.show()
    
    def plot_oxygen_trends(self, save=True):
        if len(self.oxygen_columns) < 2:
            return
        
        print(f"\nCreating trend plot...")
        
        fig, ax = plt.subplots(figsize=(10, 6))
        
        for trial_id in self.df['trial_id']:
            trial_data = self.df[self.df['trial_id'] == trial_id]
            oxygen_values = [trial_data[col].values[0] for col in self.oxygen_columns 
                           if pd.notna(trial_data[col].values[0])]
            
            if oxygen_values:
                ax.plot(range(1, len(oxygen_values)+1), oxygen_values, 
                       marker='o', label=f'Trial {trial_id}', alpha=0.7)
        
        ax.axhspan(TARGET_OXYGEN_MIN, TARGET_OXYGEN_MAX, 
                  alpha=0.2, color='green', label='Target Zone')
        
        ax.set_xlabel('Run Number')
        ax.set_ylabel('Oxygen Remaining (%)')
        ax.set_title('Oxygen Levels Across Runs')
        ax.legend(bbox_to_anchor=(1.05, 1), loc='upper left')
        ax.grid(True, alpha=0.3)
        plt.tight_layout()
        
        if save:
            output_file = Path(RESULTS_FOLDER) / 'oxygen_trends.png'
            plt.savefig(output_file, dpi=300, bbox_inches='tight')
            print(f"Saved: {output_file}")
        
        plt.show()
    
    def compare_runs(self):
        if len(self.oxygen_columns) < 2:
            return
        
        print(f"\n{'='*60}")
        print(f"COMPARING ALL RUNS")
        print(f"{'='*60}")
        
        comparison_data = []
        for col in self.oxygen_columns:
            valid_data = self.df[self.df[col].notna()][col]
            if len(valid_data) > 0:
                comparison_data.append({
                    'Run': col,
                    'Mean': valid_data.mean(),
                    'Std': valid_data.std(),
                    'Min': valid_data.min(),
                    'Max': valid_data.max(),
                    'Perfect': sum(valid_data <= TARGET_OXYGEN_MAX),
                    'Failed': sum(valid_data <= 0)
                })
        
        df_comparison = pd.DataFrame(comparison_data)
        print(f"\n{df_comparison.to_string(index=False)}")
        
        if len(self.oxygen_columns) >= 3:
            oxygen_data = [self.df[col].dropna().values for col in self.oxygen_columns]
            f_stat, p_value = stats.f_oneway(*oxygen_data)
            print(f"\nANOVA Test:")
            print(f"   F-statistic: {f_stat:.4f}")
            print(f"   P-value: {p_value:.4f}")
            if p_value < 0.05:
                print(f"   Significant difference between runs")
            else:
                print(f"   No significant difference between runs")
    
    def save_report(self, results):
        output_file = Path(RESULTS_FOLDER) / f'python_analysis_report.txt'
        output_file.parent.mkdir(exist_ok=True)
        
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write("=" * 70 + "\n")
            f.write("AQUAGUARDIAN - PYTHON REGRESSION ANALYSIS\n")
            f.write("=" * 70 + "\n\n")
            
            f.write(f"Data File: {self.data_file}\n")
            f.write(f"Runs Analyzed: {len(results)}\n")
            f.write(f"Target Oxygen: {TARGET_OXYGEN_MIN}-{TARGET_OXYGEN_MAX}%\n\n")
            
            for result in results:
                f.write("\n" + "=" * 70 + "\n")
                f.write(f"{result['oxygen_col'].upper()}\n")
                f.write("=" * 70 + "\n\n")
                
                stats = result['stats']
                f.write(f"Statistics:\n")
                f.write(f"  Mean: {stats['mean']:.2f}%\n")
                f.write(f"  Std Dev: {stats['std']:.2f}%\n")
                f.write(f"  Range: {stats['min']:.1f}% - {stats['max']:.1f}%\n")
                f.write(f"  Perfect Trials: {stats['perfect']}\n")
                f.write(f"  Failed Trials: {stats['failed']}\n\n")
                
                f.write("Correlations:\n")
                sorted_corr = sorted(result['correlations'].items(),
                                   key=lambda x: abs(x[1]['correlation']),
                                   reverse=True)
                
                for param, data in sorted_corr:
                    f.write(f"  {param:<25} {data['correlation']:>7.3f}  "
                           f"{data['strength']:<10}  p={data['p_value']:.4f}\n")
        
        print(f"\nSaved report: {output_file}")
    
    def run_full_analysis(self):
        print("\n" + "=" * 70)
        print("AQUAGUARDIAN TRIAL REGRESSION ANALYSIS")
        print("=" * 70)
        
        self.load_data()
        
        results = []
        for oxygen_col in self.oxygen_columns:
            result = self.analyze_run(oxygen_col)
            if result:
                results.append(result)
        
        if not results:
            print("\nNo valid data to analyze")
            return
        
        if len(results) > 1:
            self.compare_runs()
        
        try:
            self.plot_correlations(results)
            if len(self.oxygen_columns) > 1:
                self.plot_oxygen_trends()
        except Exception as e:
            print(f"Could not create plots: {e}")
        
        self.save_report(results)
        
        print("\n" + "=" * 70)
        print("ANALYSIS COMPLETE")
        print("=" * 70)
        print(f"\nResults: {Path(RESULTS_FOLDER).absolute()}")


def main():
    try:
        analyzer = TrialAnalyzer(DATA_FILE)
        analyzer.run_full_analysis()
    except Exception as e:
        print(f"\nError: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    main()

