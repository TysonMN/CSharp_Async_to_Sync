using System;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Nito.AsyncEx;
using Xunit;

namespace Tests {

  public static class TaskExtensions {

    /// <summary>
    /// Synchronously waits (aka blocks) in order to
    /// extract the value out of the computed Task
    /// by executing the asynchronous computation on a thread
    /// from the background thread pool.
    /// The use of this method is not ideal.
    /// If possible, await the computed Task instead.
    /// 
    /// By using a thread from the background thread pool,
    /// this method attempts to avoid a deadlock that would occur
    /// if both of the following are satisfied:
    /// (1) the synchronization context of the caller only contains a single unblocked thread
    /// (which is typically true of front-end synchronization contexts) and
    /// (2) the asynchronous computation synchronously waits (aka blocks).
    /// 
    /// See also <seealso cref="ExecuteSynchronously{A}(Task{A})"/>.
    /// </summary>
    public static A ExecuteSynchronously<A>(this Func<Task<A>> mma) =>
      Task.Run(mma).ExecuteSynchronously();

    /// <summary>
    /// Synchronously waits (aka blocks) in order to
    /// extract the value out of the given Task.
    /// The use of this method is not ideal.
    /// If possible, await the given Task instead.
    /// 
    /// More generally,
    /// instead of trying to extract the value out,
    /// inject the related behavior in:
    /// - https://blog.ploeh.dk/2019/02/04/how-to-get-the-value-out-of-the-monad/
    /// - https://www.youtube.com/watch?v=F9bznonKc64
    /// 
    /// If then given Task never completes,
    /// then this method never returns.
    /// Otherwise,
    /// suppose the Task has completed.
    /// 
    /// If the given Task is cancelled,
    /// then this method throws a TaskCanceledException.
    /// 
    /// If the given Task is faulted,
    /// then this method throws an exception.
    /// The type of this exception will be
    /// the same type thrown from the asynchronous computation.
    /// If the asynchronous computation throws more than one exception,
    /// then only the first such exception is thrown.
    /// </summary>
    public static A ExecuteSynchronously<A>(this Task<A> ma) =>
      ma.GetAwaiter().GetResult();

  }

  public class UnitTest1 {

    [Property]
    public void ExecuteSynchronouslyTask_ValueInsideIsReturned(int expected) {
      var actual = Task
        .FromResult(expected)
        .ExecuteSynchronously();
      Assert.Equal(expected, actual);
    }

    public class ExecuteSynchronouslyTestException : Exception {
      public static ExecuteSynchronouslyTestException Unit { get; } = new ExecuteSynchronouslyTestException();
    }
    [Fact]
    public void ExecuteSynchronouslyTask_FaultedTaskThrowsUnwrappedException() {
      var task = Task
        .FromException<int>(ExecuteSynchronouslyTestException.Unit);
      Assert.Throws<ExecuteSynchronouslyTestException>(
        () => task.ExecuteSynchronously());
    }

    [Property]
    public void ExecuteSynchronouslyFuncTask_DoesNotBlockSingleThreadedContext(int expected) {
      Func<Task<int>> f = async () => {
        await Task.Delay(1);
        return expected;
      };
      AsyncContext.Run(() => {
        var actual = f.ExecuteSynchronously();
        Assert.Equal(expected, actual);
      });
    }

    public class FirstException : Exception { }
    public class SecondException : Exception { }

    [Fact]
    public void ExecuteSynchronously_MultipleExceptionsAllInstant_ThrowsFirstException() {
      var task = Task
        .WhenAll(
          Task.FromException<int>(new FirstException()),
          Task.FromException<int>(new SecondException())
        );
      Assert.Throws<FirstException>(() => task.ExecuteSynchronously());
    }

    [Fact]
    public void ExecuteSynchronously_MultipleExceptionsFirstDelayed_ThrowsFirstException() {
      var task = Task
        .WhenAll(
          _ThrowFirstExceptionAfterDelay(),
          Task.FromException<int>(new SecondException())
        );
      Assert.Throws<FirstException>(() => task.ExecuteSynchronously());
    }

    private async Task<int> _ThrowFirstExceptionAfterDelay() {
      await Task.Delay(TimeSpan.FromMilliseconds(100));
      throw new FirstException();
    }

    [Fact]
    public void ExecuteSynchronously_CancellledTask_ThrowsTaskCanceledException() {
      var task = Task.FromCanceled<int>(new CancellationToken(canceled: true));
      Assert.Throws<TaskCanceledException>(() => task.ExecuteSynchronously());
    }

  }
}
