using UnityEngine;

[CreateAssetMenu(fileName = "ActionChains", menuName = "SO/Action ChainsSO")]
public class ActionChainListSO : ScriptableObject
{
    [System.Serializable]
    public class Step
    {
        [Header("Trigger")]
        public string triggerVisualId; 
        public string triggerAnimation;  

        [Header("Next")]
        public string nextVisualId;  
        public string nextAnimation;      

        [Header("Completion Options")]
        public bool isLastAnim = false;         
        public bool disableOnComplete = false; 
    }

    [System.Serializable]
    public class Chain
    {
        public string chainId;        
        public Step[] steps;            
    }

    [Header("Exactly one required chain to win")]
    public string requiredChainId;      

    [Header("Chains")]
    public Chain[] chains;
}
