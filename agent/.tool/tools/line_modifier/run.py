"""
文件局部编辑 Skill

在指定文件的指定行范围内替换内容
"""

from typing import Any

MAX_WRITE_LINES = 40
MAX_WRITE_CHARS = 4000


def run(params: dict[str, Any]) -> dict[str, Any]:
    """
    执行文件局部编辑操作

    Args:
        params: 参数字典
            - filepath: 文件路径（必需）
            - start_line: 起始行号，从1开始（必需）
            - end_line: 结束行号，从1开始（必需）
            - new_content: 新内容，将替换指定行范围（必需）

    Returns:
        dict:
            - success: bool，是否成功
            - total_lines_before: int，编辑前文件总行数
            - total_lines_after: int，编辑后文件总行数
            - modified_lines: tuple，实际修改的行范围 (起始行, 结束行)
            - written_lines: int，写入的行数
            - written_chars: int，写入的字符数
            - error: str，错误信息（如有）
    """
    # 提取参数
    filepath = params.get("filepath")
    start_line = params.get("start_line")
    end_line = params.get("end_line")
    new_content = params.get("new_content")
    
    # 参数校验
    if not filepath or start_line is None or end_line is None or new_content is None:
        return {
            "success": False, 
            "error": "缺少必要参数: filepath, start_line, end_line, new_content"
        }
    
    try:
        # 行号校验
        start_line = _positive_int(start_line, "start_line")
        end_line = _positive_int(end_line, "end_line")
        
        if start_line > end_line:
            return {"success": False, "error": "start_line 不能大于 end_line"}
        
        # 新内容校验（只限制新内容的大小，不限制操作范围）
        if not isinstance(new_content, str):
            return {"success": False, "error": "new_content 必须是字符串"}
        
        # 检查字符数限制
        if len(new_content) > MAX_WRITE_CHARS:
            return {
                "success": False, 
                "error": f"单次写入内容最多 {MAX_WRITE_CHARS} 个字符，当前 {len(new_content)} 个字符"
            }
        
        # 检查行数限制
        new_lines_count = len(new_content.splitlines())
        if new_lines_count > MAX_WRITE_LINES:
            return {
                "success": False, 
                "error": f"单次写入内容最多 {MAX_WRITE_LINES} 行，当前 {new_lines_count} 行"
            }
        
        # 读取文件
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        total_lines = len(lines)
        
        # 检查起始行是否超出范围
        if start_line > total_lines + 1:
            return {
                "success": False, 
                "error": f"start_line 超出文件范围: 文件共 {total_lines} 行，start_line={start_line}"
            }
        
        # 计算实际替换范围
        s = start_line - 1  # 转为0-based索引
        e = min(end_line, total_lines)  # 结束索引（不包含）
        
        # 处理新内容
        replacement = new_content.splitlines(keepends=True)
        if not replacement:
            # 空内容表示删除指定行
            replacement = []
        else:
            # 确保最后一行有换行符（除非文件本身就是最后一行没有换行）
            if replacement[-1] and not replacement[-1].endswith('\n'):
                replacement[-1] += '\n'
        
        # 执行替换
        lines[s:e] = replacement
        
        # 写回文件
        with open(filepath, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        
        return {
            "success": True,
            "total_lines_before": total_lines,
            "total_lines_after": len(lines),
            "modified_lines": (s + 1, e if e > 0 else s + 1),
            "written_lines": len(replacement),
            "written_chars": len(new_content),
            "error": None
        }
        
    except FileNotFoundError:
        return {"success": False, "error": f"文件不存在: {filepath}"}
    except PermissionError:
        return {"success": False, "error": f"权限不足，无法读写文件: {filepath}"}
    except UnicodeDecodeError:
        return {"success": False, "error": f"文件编码错误，请确保文件为 UTF-8 编码: {filepath}"}
    except Exception as e:
        return {"success": False, "error": f"编辑文件失败: {str(e)}"}


def _positive_int(value: Any, name: str) -> int:
    """
    校验并转换正整数
    
    Args:
        value: 待校验的值
        name: 参数名称（用于错误提示）
    
    Returns:
        int: 校验通过的正整数
    
    Raises:
        ValueError: 值不是正整数时抛出
    """
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{name} 必须是正整数")
    return value