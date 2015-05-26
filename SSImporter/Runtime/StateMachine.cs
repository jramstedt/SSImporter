using UnityEngine;
using System.Collections;

namespace SystemShock {
    public abstract class StateMachine<StateType> : MonoBehaviour {
        protected virtual void hideState(StateType nextState) {
            HideState(nextState);
        }

        protected virtual void showState(StateType previousState) {
            ShowState(previousState);
        }

        protected abstract void ShowState(StateType previousState);
        protected abstract void HideState(StateType nextState);

        private StateType state;
        public StateType State {
            protected set {
                StateType previousState = state;
                hideState(value);
                state = value;
                showState(previousState);
            }

            get {
                return state;
            }
        }
    }
}