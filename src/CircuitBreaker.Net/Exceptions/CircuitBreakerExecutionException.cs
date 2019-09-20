using System;

namespace CircuitBreaker.Net.Exceptions
{
    public class CircuitBreakerExecutionException : CircuitBreakerException
    {
        public CircuitBreakerExecutionException()
        {
        }
        public CircuitBreakerExecutionException(Exception inner) : base("CircuitBreaker action execution failed", inner)
        {

        }
        public CircuitBreakerExecutionException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}