::����������չ���μ�setlocal /?����
Setlocal ENABLEDELAYEDEXPANSION
set svn_path=svn://192.168.0.200/client-01/branches/alpha
set ch1=/
::�����ַ����������ض̣�����Ӱ��Դ�ַ���
set str=%svn_path%

:next
if not "%str%"=="" (
set /a num+=1
if "!str:~-1!"=="%ch1%" goto last
::�Ƚ����ַ��Ƿ�ΪҪ����ַ��������������ѭ��
set "str=%str:~0,-1%"
goto next
)
set /a num=0
::û���ҵ��ַ�ʱ����num����
:last
set var=%svn_path:~num%

echo �ַ�'%ch1%'���ַ���"%str1%"�е��״γ���λ��Ϊ%num%  %var%
echo �����ϣ���������˳�&&pause>nul&&exit
