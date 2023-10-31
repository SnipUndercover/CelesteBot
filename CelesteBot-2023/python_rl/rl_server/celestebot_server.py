"""
Example of running an RLlib policy server, allowing connections from
external environment running clients. The server listens on
(a simple CartPole env
in this case) against an RLlib policy server listening on one or more
HTTP-speaking ports. See `cartpole_client.py` in this same directory for how
to start any number of clients (after this server has been started).

This script will not create any actual env to illustrate that RLlib can
run w/o needing an internalized environment.

Setup:
1) Start this server:
    $ python cartpole_server.py --num-workers --[other options]
      Use --help for help.
2) Run n policy clients:
    See `cartpole_client.py` on how to do this.

The `num-workers` setting will allow you to distribute the incoming feed over n
listen sockets (in this example, between 9900 and 990n with n=worker_idx-1).
You may connect more than one policy client to any open listen port.
"""

import argparse
import logging
import os

import ray
from ray.rllib.algorithms.ppo import PPOConfig
from ray.rllib.env.policy_server_input import PolicyServerInput
from ray.rllib.evaluation.collectors.sample_collector import SampleCollector
from ray.rllib.examples.custom_metrics_and_callbacks import MyCallbacks
from ray.tune.logger import pretty_print
from ray.tune.registry import get_trainable_cls

from python_rl.rl_common.celestebot_env import CelesteEnv

SERVER_ADDRESS = "127.0.0.1"
# In this example, the user can run the policy server with
# n workers, opening up listen ports 9900 - 990n (n = num_workers - 1)
# to each of which different clients may connect.
SERVER_BASE_PORT = 9900  # + worker-idx - 1

CHECKPOINT_FILE = "last_checkpoint_{}.out"


def get_cli_args():
    """Create CLI parser and return parsed arguments"""
    parser = argparse.ArgumentParser()

    # Example-specific args.
    parser.add_argument(
        "--port",
        type=int,
        default=SERVER_BASE_PORT,
        help="The base-port to use (on localhost). " f"Default is {SERVER_BASE_PORT}.",
    )
    parser.add_argument(
        "--callbacks-verbose",
        action="store_true",
        help="Activates info-messages for different events on "
             "server/client (episode steps, postprocessing, etc..).",
    )
    parser.add_argument(
        "--num-workers",
        type=int,
        default=1,
        help="The number of workers to use. Each worker will create "
             "its own listening socket for incoming experiences.",
    )
    parser.add_argument(
        "--no-restore",
        action="store_true",
        help="Do not restore from a previously saved checkpoint (location of "
             "which is saved in `last_checkpoint_[algo-name].out`).",
    )

    # General args.

    parser.add_argument("--num-cpus", type=int, default=8)
    parser.add_argument(
        "--framework",
        choices=["tf", "tf2", "torch"],
        default="torch",
        help="The DL framework specifier.",
    )
    # parser.add_argument(
    #     "--use-lstm",
    #     action="store_true",
    #     help="Whether to auto-wrap the model with an LSTM. Only valid option for "
    #     "--run=[IMPALA|PPO|R2D2]",
    # )
    parser.add_argument(
        "--stop-iters", type=int, default=200, help="Number of iterations to train."
    )
    parser.add_argument(
        "--stop-timesteps",
        type=int,
        default=500000,
        help="Number of timesteps to train.",
    )

    # parser.add_argument(
    #     "--as-test",
    #     action="store_true",
    #     help="Whether this script should be run as a test: --stop-reward must "
    #     "be achieved within --stop-timesteps AND --stop-iters.",
    # )
    # parser.add_argument(
    #     "--no-tune",
    #     action="store_true",
    #     help="Run without Tune using a manual train loop instead. Here,"
    #     "there is no TensorBoard support.",
    # )
    # parser.add_argument(
    #     "--local-mode",
    #     action="store_true",
    #     help="Init Ray in local mode for easier debugging.",
    # )

    args = parser.parse_args()
    print(f"Running with following CLI args: {args}")
    return args


if __name__ == "__main__":
    args = get_cli_args()
    ray.init()


    # `InputReader` generator (returns None if no input reader is needed on
    # the respective worker).
    def _input(ioctx):
        # We are remote worker or we are local worker with num_workers=0:
        # Create a PolicyServerInput.
        if ioctx.worker_index > 0 or ioctx.worker.num_workers == 0:
            return PolicyServerInput(
                ioctx,
                SERVER_ADDRESS,
                args.port + ioctx.worker_index - (1 if ioctx.worker_index > 0 else 0),
                idle_timeout=0.025
            )
        # No InputReader (PolicyServerInput) needed.
        else:
            return None


    env = CelesteEnv(None)

    # Algorithm config. Note that this config is sent to the client only in case
    # the client needs to create its own policy copy for local inference.
    config = (
        PPOConfig()
        # Indicate that the Algorithm we setup here doesn't need an actual env.
        # Allow spaces to be determined by user (see below).
        .rl_module(_enable_rl_module_api=False)
        .training(_enable_learner_api=False)
        .environment(
            env=None,
            # TODO: (sven) make these settings unnecessary and get the information
            #  about the env spaces from the client.
            observation_space=env.observation_space,
            action_space=env.action_space,
        )
        # .update_from_dict({"num_gpus_per_worker": 1})
        # DL framework to use.
        .framework("torch")
        .resources(num_gpus=1, num_cpus_per_worker=2, )
        # .num_gpus(1)
        # Create a "chatty" client/server or not.
        # .callbacks(MyCallbacks if args.callbacks_verbose else None)
        # Use the `PolicyServerInput` to generate experiences.
        .offline_data(input_=_input, offline_sampling=False, shuffle_buffer_size=0)
        # Use n worker processes to listen on different ports.
        .rollouts(
            num_rollout_workers=2,
            # Connectors are not compatible with the external env.
            enable_connectors=False,
            create_env_on_local_worker=True,
            batch_mode="truncate_episodes",
            rollout_fragment_length=1,
            remote_env_batch_wait_ms=0,
        )
        # Disable OPE, since the rollouts are coming from online clients.
        .evaluation(off_policy_estimation_methods={})
        # Set to INFO so we'll see the server's actual address:port.
        .debugging(log_level="INFO")
    )

    # Example of using PPO (does NOT support off-policy actions).
    config.update_from_dict(
        {
            "rollout_fragment_length": 100,
            "train_batch_size": 400,
            "model": {"use_lstm": False},
            "count_steps_by": "env_steps"
        }
    )

    checkpoint_path = CHECKPOINT_FILE.format('PPO')
    # Attempt to restore from checkpoint, if possible.
    if not args.no_restore and os.path.exists(checkpoint_path):
        logging.log(logging.INFO, "Restoring from checkpoint path " + checkpoint_path)
        checkpoint_path = open(checkpoint_path).read()
    else:
        checkpoint_path = None

    # Manual training loop (no Ray tune).
    if True:
        algo = config.build()

        logging.getLogger('requests').setLevel(logging.CRITICAL)
        # TODO: Figure out why connections keep closing (HTTP BaseHandler is 1.0 not 1.1)
        logging.getLogger('urllib3.connectionpool').setLevel(logging.CRITICAL)

        if checkpoint_path:
            print("Restoring from checkpoint path", checkpoint_path)
            algo.restore(checkpoint_path)

        # Serving and training loop.
        ts = 0
        for _ in range(100000):
            results = algo.train()
            print(pretty_print(results))
            checkpoint = algo.save().checkpoint
            print("Last checkpoint", checkpoint)
            with open(checkpoint_path, "w") as f:
                logging.log(logging.INFO, "Saving checkpoint path " + checkpoint.path)
                f.write(checkpoint.path)
            if ts >= args.stop_timesteps:
                break
            ts += results["timesteps_total"]

        algo.stop()

    # Run with Tune for auto env and algo creation and TensorBoard.
    # else:
    #     print("Ignoring restore even if previous checkpoint is provided...")
    #
    #     stop = {
    #         "training_iteration": args.stop_iters,
    #         "timesteps_total": args.stop_timesteps,
    #         "episode_reward_mean": args.stop_reward,
    #     }
    #
    #     tune.Tuner(
    #         args.run, param_space=config, run_config=air.RunConfig(stop=stop, verbose=2)
    #     ).fit()
