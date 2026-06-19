using System;
using Amlos.AI.Nodes;
using UnityEngine;

namespace Amlos.AI
{
    public partial class BehaviourTree
    {
        /// <summary>
        /// Service update (during fixed update)
        /// </summary>
        private void ServiceUpdate()
        {
            //Debug.Log("Service Update Start :" + mainStack);
            var stacks = GetActiveStacksSnapshot();
            for (int i = 0; i < stacks.Count; i++)
            {
                var callStack = stacks[i];
                if (callStack?.Nodes == null)
                {
                    continue;
                }

                var stackNodes = callStack.Nodes.ToArray();
                for (int j = 0; j < stackNodes.Length; j++)
                {
                    TreeNode progress = stackNodes[j];
                    var serviceReferences = progress?.services;
                    if (serviceReferences == null)
                    {
                        continue;
                    }

                    for (int k = 0; k < serviceReferences.Count; k++)
                    {
                        var node = GetNode(serviceReferences[k]);
                        if (node is not Service service)
                        {
                            continue;
                        }

                        //service not found
                        if (!serviceStacks.TryGetValue(service, out var serviceStack))
                        {
                            //Log($"Service {service.name} did not load into the behaviour tree properly.");
                            continue;
                        }
                        //Log($"Service {service.name} Start");

                        DeactivateIdleServiceStack(service, serviceStack);

                        //increase service timer
                        //serviceStack.currentFrame++;
                        service.UpdateTimer();
                        if (!service.IsReady) continue;

                        serviceStack = GetOrCreateServiceStack(service);
                        RunService(service, serviceStack);
                    }
                }
            }
        }

        private NodeCallStack GetServiceStack(TreeNode node)
        {
            if (node == null)
            {
                return null;
            }

            return serviceStacks.TryGetValue(node, out var stack) ? stack : null;
        }

        private void RegistryServices(TreeNode node)
        {
            var serviceReferences = node.services;
            if (serviceReferences == null)
            {
                return;
            }

            foreach (var item in serviceReferences)
            {
                if (GetNode(item) is not Service service)
                {
                    Debug.LogError($"Invalid service reference on node [{node.name}].");
                    continue;
                }

                if (serviceStacks.ContainsKey(service))
                {
                    continue;
                }

                serviceStacks[service] = null;
                service.OnRegistered();
            }
        }

        private void RemoveServicesRegistry(TreeNode node)
        {
            var serviceReferences = node.services;
            if (serviceReferences == null)
            {
                ResetStageTimer();
                return;
            }

            foreach (var item in serviceReferences)
            {
                if (GetNode(item) is not Service service)
                {
                    continue;
                }
                // service might have been remove early
                if (!serviceStacks.ContainsKey(service))
                {
                    continue;
                }
                var stack = serviceStacks[service];
                if (stack != null)
                {
                    EndStack(stack);
                }
                serviceStacks.Remove(service);
                service.OnUnregistered();
            }
            ResetStageTimer();
        }

        private void RunService(Service service, NodeCallStack stack)
        {
            //last service hasn't finished 
            if (stack.Count != 0)
            {
                Log($"Service {service.name} did not finish executing in expect time.");
                stack.End();
            }

            //execute
            stack.Initialize();
            RegistryServices(service);
            stack.Start(service);
            DeactivateIdleServiceStack(service, stack);
            //Debug.Log("Service Complete");
        }

        private NodeCallStack GetOrCreateServiceStack(Service service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (!serviceStacks.TryGetValue(service, out var stack))
            {
                throw new ArgumentException("Given service does not exist in stacks", nameof(service));
            }

            if (stack != null)
            {
                if (!activeStacks.ContainsKey(stack))
                {
                    ActivateStack(stack, StackType.Service, GetServiceStackLabel(service));
                }
                return stack;
            }

            stack = CreateStack(StackType.Service, GetServiceStackLabel(service));
            serviceStacks[service] = stack;
            return stack;

            static string GetServiceStackLabel(Service service)
            {
                return string.IsNullOrWhiteSpace(service.name) ? "Service" : service.name;
            }
        }


        private void DeactivateIdleServiceStack(Service service, NodeCallStack stack)
        {
            if (service == null || stack == null)
            {
                return;
            }

            if (!serviceStacks.TryGetValue(service, out var registeredStack) || registeredStack != stack)
            {
                return;
            }

            if (stack.IsRunning || stack.Count > 0 || stack.State != NodeCallStack.StackState.End)
            {
                return;
            }

            // The host node still owns this service, so keep the stack cached for reuse.
            DeactivateIdleStack(stack);
        }

        /// <summary>
        /// end a service
        /// </summary>
        /// <param name="service"></param>
        internal void EndService(Service service)
        {
            var stack = GetServiceStack(service) ?? throw new ArgumentException("Given service does not have an allocated stack", nameof(service));
            stack.End();
        }
    }
}
