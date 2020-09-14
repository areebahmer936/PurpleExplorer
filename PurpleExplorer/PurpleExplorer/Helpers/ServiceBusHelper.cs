﻿using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Management;
using PurpleExplorer.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Message = PurpleExplorer.Models.Message;

namespace PurpleExplorer.Helpers
{
    public class ServiceBusHelper : IServiceBusHelper
    {
        private int _maxMessageCount = 100;

        public async Task<IList<ServiceBusTopic>> GetTopics(string connectionString)
        {
            IList<ServiceBusTopic> topics = new List<ServiceBusTopic>();
            var client = new ManagementClient(connectionString);

            var busTopics = await client.GetTopicsAsync();

            await Task.WhenAll(busTopics.Select(async t =>
            {
                var topicName = t.Path;
                var subscriptions = await GetSubscriptions(connectionString, topicName);

                var newTopic = new ServiceBusTopic
                {
                    Name = topicName,
                };

                newTopic.AddSubscriptions(subscriptions.ToArray());
                topics.Add(newTopic);
            }));

            return topics;
        }

        public async Task<IList<ServiceBusSubscription>> GetSubscriptions(string connectionString, string topicPath)
        {
            IList<ServiceBusSubscription> subscriptions = new List<ServiceBusSubscription>();
            ManagementClient client = new ManagementClient(connectionString);

            var topicSubscription = await client.GetSubscriptionsRuntimeInfoAsync(topicPath);
            foreach (var sub in topicSubscription)
            {
                subscriptions.Add(
                    new ServiceBusSubscription()
                    {
                        Name = sub.SubscriptionName,
                        MessageCount = sub.MessageCountDetails.ActiveMessageCount,
                        DLQCount = sub.MessageCountDetails.DeadLetterMessageCount,
                    }
                );
            }

            return subscriptions;
        }

        public async Task<IList<Message>> GetMessagesBySubscription(string connectionString, string topicName,
            string subscriptionName)
        {
            var messageReceiver = new MessageReceiver(connectionString,
                EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), ReceiveMode.PeekLock);
            var subscriptionMessages = await messageReceiver.PeekAsync(_maxMessageCount);

            var result = subscriptionMessages.Select(message => new Message
            {
                Content = Encoding.UTF8.GetString(message.Body), 
                Size = message.Size
            }).ToList(); 
               
            return result;
        }

        public async Task<IList<Message>> GetDlqMessages(string connectionString, string topic, string subscription)
        {
            var path = EntityNameHelper.FormatSubscriptionPath(topic, subscription);
            var deadletterPath = EntityNameHelper.FormatDeadLetterPath(path);
            var receiver = new MessageReceiver(connectionString, deadletterPath, ReceiveMode.PeekLock);
            var receivedMessages = await receiver.PeekAsync(_maxMessageCount);

            var result = receivedMessages.Select(message => new Message
            {
                Content = Encoding.UTF8.GetString(message.Body), 
                Size = message.Size
            }).ToList();
            
            return result;
        }

        public async Task<NamespaceInfo> GetNamespaceInfo(string connectionString)
        {
            var client = new ManagementClient(connectionString);
            return await client.GetNamespaceInfoAsync();
        }
    }
}