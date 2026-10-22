using NineToFight.Core;
using NineToFight.Enemies;
using NineToFight.Player;
using NineToFight.UI;
using UnityEngine;

namespace NineToFight.Game
{
    /// <summary>
    /// First-floor onboarding: one control prompt at a time, shown when it's relevant and dismissed
    /// once the player has done it. Can be switched off under Options > Tutorial hints.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        enum Step { Move, Attack, Dodge, Guard, Ability, Done }

        GameFlow flow;
        GameHud hud;
        PlayerController player;
        Step step = Step.Move;
        bool showing, wasHit, sawBlock, landedHit, gotKnockout;
        float shownAt, startX;

        void Start()
        {
            flow = GetComponent<GameFlow>();
            hud = FindAnyObjectByType<GameHud>();
            player = flow != null ? flow.Player : null;
            if (player == null || hud == null)
            {
                enabled = false;
                return;
            }
            player.Health.Damaged += _ => wasHit = true;
            Health.AnyDamaged += OnAnyDamaged;
            EnemyBrain.Callout += OnCallout;
            EnemyBrain.AnyDefeated += OnDefeated;
        }

        void OnDestroy()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            EnemyBrain.Callout -= OnCallout;
            EnemyBrain.AnyDefeated -= OnDefeated;
        }

        void OnAnyDamaged(Health target, DamageInfo info)
        {
            if (player != null && info.source == player.gameObject) landedHit = true;
        }

        void OnCallout(EnemyBrain enemy, string text, Color color)
        {
            if (text == "BLOCKED") sawBlock = true;
        }

        void OnDefeated(EnemyBrain enemy) => gotKnockout = true;

        void Update()
        {
            if (flow == null || flow.Floor != 1 || !GameSettings.Hints || flow.State == GameState.FloorCleared)
            {
                Hide();
                return;
            }
            if (flow.State != GameState.Playing) return;

            float shown = Time.time - shownAt;
            switch (step)
            {
                case Step.Move:
                    if (!showing) { startX = player.transform.position.x; Show("A D|Move;W S|Change lane;Space|Jump"); }
                    else if (Mathf.Abs(player.transform.position.x - startX) > 3f && shown > 1.5f) Advance();
                    break;
                case Step.Attack:
                    if (!showing && EnemyNear(7f)) Show("1 J|Attack;2 K|Heavy attack");
                    else if (showing && landedHit && shown > 1.2f) Advance();
                    break;
                case Step.Dodge:
                    if (!showing && wasHit) Show("Shift|Dodge through their attacks");
                    else if (showing && shown > 4f) Advance();
                    break;
                case Step.Guard:
                    if (!showing && sawBlock) Show("2 K|Heavy hits break an Accountant's guard");
                    else if (showing && shown > 4.5f) Advance();
                    else if (!showing && gotKnockout && !sawBlock) Advance(); // nothing blocked yet; move on
                    break;
                case Step.Ability:
                    if (!showing && gotKnockout) Show("Q|Use your department's ability");
                    else if (showing && (shown > 5f || (player.Ability != null && player.Ability.IsActive))) Advance();
                    break;
            }
        }

        bool EnemyNear(float range)
        {
            foreach (var e in EnemyBrain.Active)
                if (Mathf.Abs(e.transform.position.x - player.transform.position.x) < range) return true;
            return false;
        }

        void Show(string spec)
        {
            hud.ShowHint(spec);
            showing = true;
            shownAt = Time.time;
        }

        void Hide()
        {
            if (!showing) return;
            hud.HideHint();
            showing = false;
        }

        void Advance()
        {
            Hide();
            step++;
            if (step >= Step.Done) enabled = false;
        }
    }
}
