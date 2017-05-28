﻿using SocialTalents.Hp.Events.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SocialTalents.Hp.Events
{
    public delegate void Delegate<TArg>(TArg param);
    public delegate DateTime ExecutionTimeCalculator();

    /// <summary>
    /// EventBus used to Subcsribe to handle events and Publish them
    /// See https://github.com/Socialtalents/SocialTalents.hp for sources and samples
    /// </summary>
    public static class EventBus
    {
        private static ConcurrentDictionary<Type, object> _subscribers = new ConcurrentDictionary<Type, object>();
        private static ConcurrentDictionary<Type, NoSubscribers> _noSubscribersRegistry = new ConcurrentDictionary<Type, NoSubscribers>();
        
        /// <summary>
        /// MaxSubscriptionsPerEvent used to catch possible developers errors, when they assign subscribers in loop, etc. Default value is 20.
        /// </summary>
        public static int MaxSubscriptionsPerEventType { get; set; } = 20;
        
        /// <summary>
        /// Subscribe handler to specific TEvent type
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="handler">delegate to handle event</param>
        public static void Subscribe<TEvent>(Delegate<TEvent> handler)
        {
            object workflowAsObject = _subscribers.GetOrAdd(typeof(TEvent), (i) => { return new SubscribersList<TEvent>(); });
            lock (workflowAsObject)
            {
                SubscribersList<TEvent> workflow = (SubscribersList<TEvent>)workflowAsObject;
                if (workflow._subscribers.Count >= MaxSubscriptionsPerEventType)
                    throw new InvalidOperationException(
                        string.Format("More than {0} subscriptions per event is not allowed by default to prevent possible errors. Increase MaxSubscriptionsPerEvent or check your code for errors", MaxSubscriptionsPerEventType));

                workflow.OnEvent += handler;
            }
        }

        /// <summary>
        /// Subscribe handler to specific TEvent type by interface
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="handler"></param>
        public static void Subscribe<TEvent>(ICanHandle<TEvent> handler)
        {
            Subscribe<TEvent>(handler.Handle);
        }

        /// <summary>
        /// Publish event of type TEvent for handling.
        /// When there are no subscribers for this event NoSubscribers event raised
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <param name="eventInstance">Event to publish</param>
        /// <param name="sender">sender instance</param>
        public static void Publish<TEvent>(TEvent eventInstance, ICanPublish<TEvent> sender)
        {
            if (sender == null)
            {
                // there is no any formal need to have sender in Publish method yet
                // Idea behid this requirement is to mark all classes which can publish events with interface so they can be discoverable 
                // (With or without source code)
                throw new ArgumentException("sender could not be null");
            }

            object outValue = null;
            if (_subscribers.TryGetValue(typeof(TEvent), out outValue))
            {
                SubscribersList<TEvent> chain = (SubscribersList<TEvent>)outValue;
                if (chain != null)
                {
                    chain.Execute(eventInstance);
                }
            }
            else
            {
                if (typeof(TEvent) != typeof(NoSubscribers))
                {
                    Publish<NoSubscribers>(countNoSubscribers<TEvent>(eventInstance), _NoSubscriberSource);
                }   
            }
        }

        internal delegate void OnEventDelegate(Type e);
        internal static ICanPublish<NoSubscribers> _NoSubscriberSource = new SenderStub<NoSubscribers>();

        /// <summary>
        /// Clear all wokrflows state
        /// </summary>
        public static void Clear()
        {
            _subscribers = new ConcurrentDictionary<Type, object>();
            _noSubscribersRegistry = new ConcurrentDictionary<Type, NoSubscribers>();
        }

        private static NoSubscribers countNoSubscribers<TEvent>(TEvent data)
        {
            NoSubscribers result = _noSubscribersRegistry.GetOrAdd(typeof(TEvent), new NoSubscribers());
            result.Counter++;
            result.LastEvent = data;
            return result;
        }
    }
}
