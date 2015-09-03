﻿// -----------------------------------------------------------------------
// <copyright file="MainLoop.cs" company="Steven Kirk">
// Copyright 2014 MIT Licence. See licence.md for more information.
// </copyright>
// -----------------------------------------------------------------------

namespace Perspex.Win32.Threading
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NGenerics.DataStructures.Queues;
    using Perspex.Platform;
    using Perspex.Threading;
    using Splat;

    /// <summary>
    /// A main loop in a <see cref="Dispatcher"/>.
    /// </summary>
    internal class MainLoop
    {
        private static IPlatformThreadingInterface platform;

        private PriorityQueue<Job, DispatcherPriority> queue =
            new PriorityQueue<Job, DispatcherPriority>(PriorityQueueType.Maximum);

        /// <summary>
        /// Initializes static members of the <see cref="MainLoop"/> class.
        /// </summary>
        static MainLoop()
        {
            platform = Locator.Current.GetService<IPlatformThreadingInterface>();
        }

        /// <summary>
        /// Runs the main loop.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token used to exit the main loop.
        /// </param>
        public void Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RunJobs();

                platform.ProcessMessage();
            }
        }

        /// <summary>
        /// Runs continuations pushed on the loop.
        /// </summary>
        public void RunJobs()
        {
            Job job = null;

            while (job != null || this.queue.Count > 0)
            {
                if (job == null)
                {
                    lock (this.queue)
                    {
                        job = this.queue.Dequeue();
                    }
                }

                if (job.Priority < DispatcherPriority.Input && platform.HasMessages())
                {
                    break;
                }

                if (job.TaskCompletionSource == null)
                {
                    job.Action();
                }
                else
                {
                    try
                    {
                        job.Action();
                        job.TaskCompletionSource.SetResult(null);
                    }
                    catch (Exception e)
                    {
                        job.TaskCompletionSource.SetException(e);
                    }
                }

                job = null;
            }
        }

        /// <summary>
        /// Invokes a method on the main loop.
        /// </summary>
        /// <param name="action">The method.</param>
        /// <param name="priority">The priority with which to invoke the method.</param>
        /// <returns>A task that can be used to track the method's execution.</returns>
        public Task InvokeAsync(Action action, DispatcherPriority priority)
        {
            var job = new Job(action, priority, false);
            this.AddJob(job);
            return job.TaskCompletionSource.Task;
        }

        /// <summary>
        /// Post action that will be invoked on main thread
        /// </summary>
        /// <param name="action">The method.</param>
        /// 
        /// <param name="priority">The priority with which to invoke the method.</param>
        internal void Post(Action action, DispatcherPriority priority)
        {
            this.AddJob(new Job(action, priority, true));
        }

        private void AddJob(Job job)
        {
            lock (this.queue)
            {
                this.queue.Add(job, job.Priority);
            }
            platform.Wake();
        }

        /// <summary>
        /// A job to run.
        /// </summary>
        private class Job
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Job"/> class.
            /// </summary>
            /// <param name="action">The method to call.</param>
            /// <param name="priority">The job priority.</param>
            /// <param name="throwOnUiThread">Do not wrap excepption in TaskCompletionSource</param>
            public Job(Action action, DispatcherPriority priority, bool throwOnUiThread)
            {
                this.Action = action;
                this.Priority = priority;
                this.TaskCompletionSource = throwOnUiThread ? null : new TaskCompletionSource<object>();
            }

            /// <summary>
            /// Gets the method to call.
            /// </summary>
            public Action Action { get; }

            /// <summary>
            /// Gets the job priority.
            /// </summary>
            public DispatcherPriority Priority { get; }

            /// <summary>
            /// Gets the task completion source.
            /// </summary>
            public TaskCompletionSource<object> TaskCompletionSource { get; }
        }
    }
}
