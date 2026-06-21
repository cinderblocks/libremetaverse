// GestureManager has been removed. Chat-based gesture trigger detection is application-layer
// logic. Clients that need it should subscribe to outgoing chat events and call
// AgentManager.PlayGesture(UUID) directly when a trigger word is detected.
