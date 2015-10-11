using System;
using System.Collections.Generic;
using System.Linq;
using SystemShock.Object;

namespace SystemShock {

    public interface IBusMessage {

    }

    public abstract class BaseMessage : IBusMessage {
        public BaseMessage() { }
    }

    public class GenericMessage<T> : BaseMessage {
        public readonly T Payload;

        public GenericMessage(T payload) : base() {
            Payload = payload;
        }
    }

    public class BusMessage : BaseMessage {
        public readonly SystemShockObject Sender;
        public BusMessage(SystemShockObject sender) {
            this.Sender = sender;
        }
    }
    public class GenericBusMessage<T> : BusMessage {
        public readonly T Payload;
        public GenericBusMessage(SystemShockObject sender, T payload) : base(sender) {
            Payload = payload;
        }

        public static explicit operator GenericMessage<T>(GenericBusMessage<T> message) {
            return new GenericMessage<T>(message.Payload);
        }
    }

    public sealed class MessageBusToken {
        private Type messageType;

        public MessageBusToken(Type messageType) {
            if (!typeof(IBusMessage).IsAssignableFrom(messageType))
                throw new ArgumentOutOfRangeException("messageType");
        }
    }


    public sealed class MessageBus : AbstractGameController<MessageBus> {
        private readonly List<IMessageBusSubscription> subscriptions = new List<IMessageBusSubscription>();

        public MessageBusToken Receive<TMessage>(Action<TMessage> deliveryAction) where TMessage : class, IBusMessage {
            if (deliveryAction == null)
                throw new ArgumentNullException("deliveryAction");

            var subscriptionToken = new MessageBusToken(typeof(TMessage));

            IMessageBusSubscription subscription = new StrongBusSubscription<TMessage>(subscriptionToken, deliveryAction);

            subscriptions.Add(subscription);

            return subscriptionToken;
        }

        public void Send<TMessage>(TMessage message) where TMessage : class, IBusMessage {
            if (message == null)
                throw new ArgumentNullException("message");

            List<IMessageBusSubscription> currentlySubscribed = (from sub in subscriptions
                                                                 where sub != null && typeof(TMessage).IsAssignableFrom(sub.GetType())
                                                                 select sub).ToList();

            currentlySubscribed.ForEach(sub => sub.Deliver(message));
        }

        public void StopReceiving(MessageBusToken token) {
            if (token == null)
                throw new ArgumentNullException("subscriptionToken");

            var currentlySubscribed = (from sub in subscriptions
                                       where object.ReferenceEquals(sub.Token, token)
                                       select sub).ToList();

            currentlySubscribed.ForEach(sub => subscriptions.Remove(sub));
        }

        public void SendAsync<TMessage>(TMessage message, AsyncCallback callback) where TMessage : class, IBusMessage {
            Action publishAction = () => { Send<TMessage>(message); };
            publishAction.BeginInvoke(callback, null);
        }

        private interface IMessageBusSubscription {
            MessageBusToken Token { get; }
            void Deliver(IBusMessage message);
        }

        private class StrongBusSubscription<TMessage> : IMessageBusSubscription where TMessage : class, IBusMessage {
            public MessageBusToken Token { get; private set; }

            private Action<TMessage> deliveryAction;

            public StrongBusSubscription(MessageBusToken token, Action<TMessage> action) {
                if (token == null)
                    throw new ArgumentNullException("token");

                if (action == null)
                    throw new ArgumentNullException("action");

                Token = token;
                deliveryAction = action;
            }

            public void Deliver(IBusMessage message) {
                if (!(message is TMessage))
                    throw new ArgumentException("Message is not the correct type");

                deliveryAction.Invoke(message as TMessage);
            }
        }
    }

}
