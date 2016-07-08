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
        public MessageBusToken() { }
    }

    public sealed class MessageBus : AbstractGameController<MessageBus> {
        private readonly List<IMessageBusSubscription> subscriptions = new List<IMessageBusSubscription>();

        public MessageBusToken Receive<TMessage>(Action<TMessage> deliveryAction) where TMessage : class, IBusMessage {
            if (deliveryAction == null)
                throw new ArgumentNullException("deliveryAction");

            var subscriptionToken = new MessageBusToken();

            IMessageBusSubscription subscription = new StrongBusSubscription<TMessage>(subscriptionToken, deliveryAction);

            subscriptions.Add(subscription);

            return subscriptionToken;
        }

        public void Send<TMessage>(TMessage message) where TMessage : class, IBusMessage {
            if (message == null)
                throw new ArgumentNullException("message");

            var currentlySubscribed = subscriptions.Where(sub => typeof(TMessage).IsAssignableFrom(sub.messageType));

            foreach (var sub in currentlySubscribed)
                sub.Deliver(message);
        }

        public void StopReceiving(MessageBusToken token) {
            if (token == null)
                throw new ArgumentNullException("token");

            var currentlySubscribed = subscriptions.FirstOrDefault(sub => object.ReferenceEquals(sub.Token, token));

            if(currentlySubscribed != null)
                subscriptions.Remove(currentlySubscribed);
        }

        public void SendAsync<TMessage>(TMessage message, AsyncCallback callback) where TMessage : class, IBusMessage {
            Action publishAction = () => Send<TMessage>(message);
            publishAction.BeginInvoke(callback, null);
        }

        private interface IMessageBusSubscription {
            MessageBusToken Token { get; }
            Type messageType { get; }
            void Deliver(IBusMessage message);
        }

        private class StrongBusSubscription<TMessage> : IMessageBusSubscription where TMessage : class, IBusMessage {
            public MessageBusToken Token { get; private set; }
            public Type messageType { get; private set; }

            private Action<TMessage> deliveryAction;

            public StrongBusSubscription(MessageBusToken token, Action<TMessage> action) {
                if (token == null)
                    throw new ArgumentNullException("token");

                if (action == null)
                    throw new ArgumentNullException("action");

                Token = token;
                deliveryAction = action;
                messageType = typeof(TMessage);
            }

            public void Deliver(IBusMessage message) {
                if (!(message is TMessage))
                    throw new ArgumentException("Message is not the correct type");

                deliveryAction.Invoke(message as TMessage);
            }
        }
    }
}
