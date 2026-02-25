namespace Amlos.AI.Nodes
{
    public class Subtree : Action
    {
        public BehaviourTreeData behaviourTreeData;
        public VariableTableTranslationBuilder variableTable;

        private BehaviourTree tree;
        private bool hasStarted;

        /// <summary>
        /// Gets the runtime behaviour tree instance created for this subtree.
        /// </summary>
        public BehaviourTree RuntimeTree => tree;

        public override void Awake()
        {
            hasStarted = false;
            VariableTranslationTable variableTranslations = variableTable.Build(behaviourTree.Variables);
            tree = new BehaviourTree(behaviourTreeData, variableTranslations, gameObject, Script);
        }

        public override void Update()
        {
            if (CheckEnding()) return;
            if (tree.IsRunning)
            {
                tree.Update();
                CheckEnding();
            }
        }

        public override void FixedUpdate()
        {
            if (!hasStarted)
            {
                if (tree.IsInitialized && !tree.IsRunning)
                {
                    tree.Start();
                    hasStarted = true;
                }
            }
            if (CheckEnding()) return;
            if (tree.IsRunning)
            {
                tree.FixedUpdate();
                CheckEnding();
            }
        }

        public override void LateUpdate()
        {
            if (CheckEnding()) return;
            if (tree.IsRunning)
            {
                tree.LateUpdate();
                CheckEnding();
            }
        }

        public override void OnDestroy()
        {
            tree.End();
        }

        public bool CheckEnding()
        {
            if (!tree.IsInitialized)
                return false;
            if (tree.IsError)
            {
                End(false);
                return true;
            }

            if (!hasStarted)
                return false;

            if (tree.IsRunning)
                return false;

            // end with exception
            if (tree.MainStack?.Exception != null)
            {
                Exception(tree.MainStack.Exception);
                return true;
            }
            // normal end, return true
            End(true);
            return true;
        }
    }
}
