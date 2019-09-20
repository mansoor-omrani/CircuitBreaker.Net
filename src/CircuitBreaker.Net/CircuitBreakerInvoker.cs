using System;
using System.Threading;
using System.Threading.Tasks;

using CircuitBreaker.Net.Exceptions;
using CircuitBreaker.Net.States;

namespace CircuitBreaker.Net
{
    internal class CircuitBreakerInvoker : ICircuitBreakerInvoker
    {
        private readonly TaskScheduler _taskScheduler;

        private Timer _timer;

        public CircuitBreakerInvoker(TaskScheduler taskScheduler)
        {
            _taskScheduler = taskScheduler;
        }

        public void InvokeScheduled(Action action, TimeSpan interval)
        {
            if (action == null) throw new ArgumentNullException("action");

            _timer = new Timer(_ => action(), null, (int)interval.TotalMilliseconds, Timeout.Infinite);
        }

        public void InvokeThrough(ICircuitBreakerState state, Action action, TimeSpan timeout)
        {
            try
            {
                Invoke(action, timeout);
            }
            catch (Exception)
            {
                state.InvocationFails();
                throw;
            }

            state.InvocationSucceeds();
        }

        public T InvokeThrough<T>(ICircuitBreakerState state, Func<T> func, TimeSpan timeout)
        {
            T result;
            try
            {
                result = Invoke(func, timeout);
            }
            catch (Exception)
            {
                state.InvocationFails();
                throw;
            }

            state.InvocationSucceeds();
            return result;
        }

        public async Task InvokeThroughAsync(ICircuitBreakerState state, Func<Task> func, TimeSpan timeout)
        {
            try
            {
                await InvokeAsync(func, timeout);
            }
            catch (Exception)
            {
                state.InvocationFails();
                throw;
            }

            state.InvocationSucceeds();
        }

        public async Task<T> InvokeThroughAsync<T>(ICircuitBreakerState state, Func<Task<T>> func, TimeSpan timeout)
        {
            Task<T> task;
            try
            {
                task = InvokeAsync(func, timeout);
                await task;
            }
            catch (Exception)
            {
                state.InvocationFails();
                throw;
            }

            state.InvocationSucceeds();

            return await task;
        }

        private void Invoke(Action action, TimeSpan timeout)
        {
            if (action == null) throw new ArgumentNullException("action");

            Task task = null;

            try
            {
                task = Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);

                if (task.Wait((int)timeout.TotalMilliseconds))
                {
                    return;
                }
            }
            catch (AggregateException e)
            {
                throw new CircuitBreakerExecutionException(e.InnerException);
            }
            catch (Exception e)
            {
                throw new CircuitBreakerExecutionException(e);
            }

            if (task.IsFaulted)
                throw new CircuitBreakerExecutionException(task.Exception.InnerException);
            else
                throw new CircuitBreakerTimeoutException();
        }


        private async Task InvokeAsyncOld(Func<Task> func, TimeSpan timeout)
        {
            if (func == null) throw new ArgumentNullException("func");

            await Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, _taskScheduler).Unwrap().TimeoutAfter(timeout);
        }
        private Task InvokeAsync(Func<Task> func, TimeSpan timeout)
        {
            if (func == null) throw new ArgumentNullException("func");

            var tcs = new TaskCompletionSource<Task>();
            var task = Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);

            Task.Delay(timeout).ContinueWith(ant =>
            {
                if (task.IsFaulted)
                {
                    tcs.SetException(new CircuitBreakerExecutionException(task.Exception.InnerException));
                }
                
                if (!task.IsCompleted)
                {
                    tcs.SetException(new CircuitBreakerTimeoutException());
                }

                if (task.Result.Status == TaskStatus.RanToCompletion)
                {
                    tcs.SetResult(task.Result);
                }
                else
                {
                    if (task.Result.IsFaulted)
                    {
                        tcs.SetException(new CircuitBreakerExecutionException(task.Result.Exception.InnerException));
                    }
                    else
                    {
                        tcs.SetException(new CircuitBreakerTimeoutException());
                    }
                }
            });

            return tcs.Task;
        }
        private async Task<T> InvokeAsyncOld<T>(Func<Task<T>> func, TimeSpan timeout)
        {
            if (func == null) throw new ArgumentNullException("func");

            return await Task<Task<T>>.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, _taskScheduler).Unwrap().TimeoutAfter(timeout);
        }
        private Task<T> InvokeAsync<T>(Func<Task<T>> func, TimeSpan timeout)
        {
            return InvokeFirstAsync(func, timeout).Unwrap();
        }
        private Task<Task<T>> InvokeFirstAsync<T>(Func<Task<T>> func, TimeSpan timeout)
        {
            if (func == null) throw new ArgumentNullException("func");

            var tcs = new TaskCompletionSource<Task<T>>();
            var task = Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);

            Task.Delay(timeout).ContinueWith(ant =>
            {
                if (task.IsFaulted)
                {
                    tcs.SetException(new CircuitBreakerExecutionException(task.Exception.InnerException));
                }

                if (!task.IsCompleted)
                {
                    tcs.SetException(new CircuitBreakerTimeoutException());
                }

                if (task.Result.Status == TaskStatus.RanToCompletion)
                {
                    tcs.SetResult(task.Result);
                }
                else
                {
                    if (task.Result.IsFaulted)
                    {
                        tcs.SetException(new CircuitBreakerExecutionException(task.Result.Exception.InnerException));
                    }
                    else
                    {
                        tcs.SetException(new CircuitBreakerTimeoutException());
                    }
                }
            });

            return tcs.Task;
        }
        private T Invoke<T>(Func<T> func, TimeSpan timeout)
        {
            if (func == null) throw new ArgumentNullException("func");

            Task<T> task = null;

            try
            {
                task = Task<T>.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);

                if (task.Wait((int)timeout.TotalMilliseconds))
                {
                    return task.Result;
                }
            }
            catch (AggregateException e)
            {
                throw new CircuitBreakerExecutionException(e.InnerException);
            }
            catch (Exception e)
            {
                throw new CircuitBreakerExecutionException(e);
            }

            if (task.IsFaulted)
                throw new CircuitBreakerExecutionException(task.Exception.InnerException);
            else
                throw new CircuitBreakerTimeoutException();
        }
    }
}