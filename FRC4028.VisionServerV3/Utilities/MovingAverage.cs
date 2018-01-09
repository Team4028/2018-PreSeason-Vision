using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRC4028.VisionServerV3.Utilities
{
    /// <summary>
    /// Calculate the Moving Average of a series of numbers.
    /// </summary>
    public class MovingAverage
    {

        Queue<int> _window;
        int _windowSize;
        int _sum;
        int _currentMovingAverage;

        public MovingAverage(int windowSize)
        {
            _window = new Queue<int>();
            _windowSize = windowSize;
        }

        public double AddSample(int nextValue)
        {
            if (_window.Count >= _windowSize)
            {
                _sum -= _window.Dequeue();
            }

            _window.Enqueue(nextValue);
            _sum += nextValue;

            _currentMovingAverage = (int)((double)_sum / (double)_window.Count);

            return _currentMovingAverage;
        }

        public int Current
        {
            get { return _currentMovingAverage; }
        }
    }
}
