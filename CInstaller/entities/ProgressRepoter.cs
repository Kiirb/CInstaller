using System.Windows.Controls;

namespace CInstaller.entities;

public class ProgressReporter(ProgressBar bar, Label label)
{
    private double _baseProgress;
    private double _stepSize = 100;
    private double _lastProgress = -1;

    public void Report(double progress, string message)
    {
        double total = _baseProgress + progress * _stepSize / 100;

        if (Math.Abs(total - _lastProgress) < 0.5)
            return;

        _lastProgress = total;

        bar.Dispatcher.BeginInvoke(() =>
        {
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
    
    public void Complete(string message)
    {
        bar.Dispatcher.BeginInvoke(() =>
        {
            bar.Value = 100;
            label.Content = message;
        });
    }
}