#ifndef _ZMQ_API_QUEUE_STATION_H
#define _ZMQ_API_QUEUE_STATION_H
#pragma once
#include "../sqlite/queue_storage.h"
#include "zero_station.h"

namespace agebull
{
	namespace zero_net
	{
		/**
		* \brief 表示一个通知站点
		*/
		class queue_station :public zero_station
		{
			queue_storage storage_;
		public:
			/**
			 * \brief 构造
			 * \param name
			 */
			queue_station(string name)
				: zero_station(std::move(name), zero_def::station_type::queue, ZMQ_ROUTER, ZMQ_PUB)
			{
			}

			/**
			 * \brief 构造
			 * \param config
			 */
			queue_station(shared_ptr<station_config>& config)
				: zero_station(config, zero_def::station_type::queue, ZMQ_ROUTER, ZMQ_PUB)
			{
			}

			/**
			* \brief 析构
			*/
			virtual  ~queue_station()
			{
				cout << "queue_station destory" << endl;
			}

			/**
			* \brief 工作开始 : 处理请求数据
			*/
			void job_start(zmq_handler socket, vector<shared_char>& list, bool inner, bool old) final;

			/**
			* \brief 运行一个通知线程
			*/
			static void run(string name)
			{
				boost::thread(launch, make_shared<queue_station>(name));
			}
			/**
			*\brief 运行
			*/
			static void run(shared_ptr<station_config>& config)
			{
				if (config->is_state(station_state::stop))
					return;
				boost::thread(boost::bind(launch, std::make_shared<queue_station>(config)));
			}
			/**
			*\brief 运行一个通知线程
			*/
			static void launch(shared_ptr<queue_station> station);
		private:
			/**
			* \brief 内部命令
			*/
			bool simple_command(zmq_handler socket, vector<shared_char>& list, shared_char& description, bool inner) final;

			/**
			* \brief 内部命令
			*/
			static void async_replay(queue_station* queue, int64 min, int64 max);
		};
	}
}
#endif//!_ZMQ_API_QUEUE_STATION_H