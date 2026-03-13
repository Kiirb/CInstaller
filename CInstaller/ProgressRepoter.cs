using System.Windows.Controls;

namespace CInstaller;

public class ProgressReporter(ProgressBar bar, Label label)
{
    private double _baseProgress;
    private double _stepSize;
    
    public void Report(double progress, string message)
    {
        bar.Dispatcher.Invoke(() =>
        {
            var total = _baseProgress + progress * _stepSize / 100;
            bar.Value = Math.Min(100, total);
            label.Content = message;
        });
    }

    public void NextStep(double stepSize)
    {
        _stepSize = stepSize;
    }

    public void FinishStep()
    {
        _baseProgress += _stepSize;
    }
}