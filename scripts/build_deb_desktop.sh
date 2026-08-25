# 创建build_root目录
# mkdir build_root
echo "准备文件..."
rm -rf /home/riverbird/builder/bool_download_build_root/usr/local/bool-download/*
cp -r /home/riverbird/project/bool-download/BoolDownload.Desktop/bin/Release/net10.0/linux-x64/publish/* /home/riverbird/builder/bool_download_build_root/usr/local/bool-download/

cp /home/riverbird/project/bool-download/BoolDownload/Assets/Icon.png /home/riverbird/builder/bool_download_build_root/usr/local/bool-download/bool-download-icon.png
cp /home/riverbird/project/bool-download/BoolDownload/Assets/Icon.png /home/riverbird/builder/bool_download_build_root/usr/share/icons/bool-download-icon.png

cp /home/riverbird/project/bool-download/scripts/bool-download.desktop /home/riverbird/builder/bool_download_build_root/usr/local/bool-download/
cp /home/riverbird/project/bool-download/scripts/bool-download.desktop /home/riverbird/builder/bool_download_build_root/usr/share/applications/

echo "开始打包..."
fpm -s dir -t deb \
  -n booldownload \
  -v 1.2.0 \
  --iteration 1.el7 \
  --prefix / \
  --description "基于迅雷下载引擎开发的跨平台下载工具。" \
  --maintainer "riverbird@aliyun.com" \
  --url "http://www.zjsbt.cn/service/derivatives" \
  -C /home/riverbird/builder/bool_download_build_root 

 # Check desktop file
 # rpm -qpl BoolHub-3.9.0-1.el7.x86_64.rpm | grep desktop

