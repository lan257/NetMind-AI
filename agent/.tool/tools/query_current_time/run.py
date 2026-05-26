from typing import Any
from datetime import datetime

def main(params: dict) -> dict:
    """
    查询当前时间，返回年月日时分秒
    """
    try:
        now = datetime.now()
        current_time = now.strftime("%Y年%m月%d日 %H时%M分%S秒")
        return {
            "status": "success",
            "result": {
                "current_time": current_time,
                "year": now.year,
                "month": now.month,
                "day": now.day,
                "hour": now.hour,
                "minute": now.minute,
                "second": now.second
            }
        }
    except Exception as e:
        return {
            "status": "error",
            "error": str(e)
        }
