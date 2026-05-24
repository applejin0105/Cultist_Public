using Domain.State.Host;
using Effects.Commands.Card;
using Effects.Commands.Flow;
using Effects.Commands.Phase;
using Effects.Commands.Resource;
using Effects.Commands.Turn;
using Effects.Conditions;
using Effects.Interfaces;
using Systems;

namespace Effects.Core
{
    /// <summary>
    /// 명령어/조건 등록 + EffectRunner 조립
    ///
    /// 사용법:
    ///   var bootstrap = new EffectsBootstrap(gameState, actionSystem, input, rng, phase);
    ///   actionSystem.SetEffectRunner(bootstrap.Runner);
    /// </summary>
    public sealed class EffectsBootstrap
    {
        public CommandRegistry Commands { get; }
        public ConditionRegistry Conditions { get; }
        public TargetResolver Targets { get; }
        public EffectRunner Runner { get; }

        public EffectsBootstrap(
            GameState gameState,
            GameActionSystem actionSystem,
            IPlayerInputProvider input,
            IRandomSource rng,
            GameRuleSystem gameRuleSystem)
        {
            Commands = new CommandRegistry();
            Conditions = new ConditionRegistry();
            Targets = new TargetResolver(gameState, input, rng);

            // 명령어 등록
            Commands.Register("Log", new LogCommand());
            Commands.Register("Get", new GetCommand(gameState, Targets));
            Commands.Register("SetNextDraw", new SetNextDrawCommand(gameState, Targets));
            Commands.Register("Draw", new DrawCommand(actionSystem, Targets));
            Commands.Register("Destroy", new DestroyCommand(gameState, actionSystem, Targets, rng));
            Commands.Register("Exile", new ExileCommand(gameState, actionSystem, Targets, rng));
            Commands.Register("Sacrifice", new SacrificeCommand(gameState, actionSystem, Targets, rng));
            Commands.Register("Trade", new TradeCommand(actionSystem, Targets));
            Commands.Register("Starve", new StarveCommand(actionSystem, Targets));
            Commands.Register("AddTurnCycle", new AddTurnCycleCommand(gameState));
            Commands.Register("If", new IfCommand(Conditions));
            Commands.Register("SetVar", new SetVarCommand(Targets));
            Commands.Register("Reveal", new RevealCommand(actionSystem, Targets));

            Conditions.Register("Compare", new CompareCondition());

            actionSystem.SetConditionRegistry(Conditions);

            Runner = new EffectRunner(gameState, Commands);
            Runner.SetGameRuleSystem(gameRuleSystem);
        }
    }
}